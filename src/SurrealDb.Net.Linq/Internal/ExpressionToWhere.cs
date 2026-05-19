using System.Collections;
using System.Linq.Expressions;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Translates an <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c> predicate into a
/// SurrealQL fragment, binding every closed-over value as a parameter on the
/// supplied <see cref="ParameterBag"/>. MVP scope: comparison + logical
/// operators, member access (including nested chains <c>u.Address.City</c>),
/// boolean member shorthand (<c>u =&gt; u.Active</c>), string
/// <c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c>, <c>string.IsNullOrEmpty</c>,
/// and collection <c>Contains</c> for <c>IN</c>/<c>CONTAINS</c>. Anything else
/// throws <see cref="NotSupportedException"/> — fall back to <c>WhereRaw</c>.
/// </summary>
internal static class ExpressionToWhere
{
    public static string Translate(LambdaExpression lambda, ParameterBag bag)
    {
        var visitor = new Visitor(lambda.Parameters, bag);
        return visitor.VisitBool(lambda.Body);
    }

    private sealed class Visitor
    {
        private readonly HashSet<ParameterExpression> _params;
        private readonly ParameterBag _bag;

        public Visitor(IEnumerable<ParameterExpression> @params, ParameterBag bag)
        {
            _params = new HashSet<ParameterExpression>(@params);
            _bag = bag;
        }

        public string VisitBool(Expression expr)
        {
            while (expr is UnaryExpression { NodeType: ExpressionType.Convert } cu && cu.Type == typeof(bool))
            {
                expr = cu.Operand;
            }

            switch (expr)
            {
                case BinaryExpression { NodeType: ExpressionType.AndAlso } a:
                    return $"({VisitBool(a.Left)} AND {VisitBool(a.Right)})";
                case BinaryExpression { NodeType: ExpressionType.OrElse } o:
                    return $"({VisitBool(o.Left)} OR {VisitBool(o.Right)})";
                case BinaryExpression b:
                    return VisitComparison(b);
                case UnaryExpression { NodeType: ExpressionType.Not } u:
                    return $"NOT ({VisitBool(u.Operand)})";
                case MethodCallExpression m:
                    return VisitMethodCall(m);
                case MemberExpression me when IsParameterChain(me) && me.Type == typeof(bool):
                    return $"{ResolveField(me)} = {_bag.Add(true)}";
                case ConstantExpression { Value: bool bv }:
                    return bv ? "true" : "false";
                default:
                    if (TryEvaluate(expr, out var val) && val is bool bb)
                    {
                        return bb ? "true" : "false";
                    }
                    throw NotSupported(expr, "expected a boolean expression");
            }
        }

        private string VisitComparison(BinaryExpression b)
        {
            var (lhs, rhs, op) = NormalizeSides(b);

            if (TryEvaluate(rhs, out var rval) && rval is null
                && lhs is MemberExpression lme && IsParameterChain(lme))
            {
                return op switch
                {
                    ExpressionType.Equal    => $"{ResolveField(lme)} IS NONE",
                    ExpressionType.NotEqual => $"{ResolveField(lme)} IS NOT NONE",
                    _ => throw NotSupported(b, $"operator {op} cannot be used with null")
                };
            }

            return $"{RenderOperand(lhs)} {RenderOp(op)} {RenderOperand(rhs)}";
        }

        private (Expression lhs, Expression rhs, ExpressionType op) NormalizeSides(BinaryExpression b)
        {
            if (!ContainsParameter(b.Left) && ContainsParameter(b.Right))
            {
                return (b.Right, b.Left, Flip(b.NodeType));
            }
            return (b.Left, b.Right, b.NodeType);
        }

        private static ExpressionType Flip(ExpressionType op) => op switch
        {
            ExpressionType.LessThan           => ExpressionType.GreaterThan,
            ExpressionType.LessThanOrEqual    => ExpressionType.GreaterThanOrEqual,
            ExpressionType.GreaterThan        => ExpressionType.LessThan,
            ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
            _ => op,
        };

        private static string RenderOp(ExpressionType op) => op switch
        {
            ExpressionType.Equal              => "=",
            ExpressionType.NotEqual           => "!=",
            ExpressionType.LessThan           => "<",
            ExpressionType.LessThanOrEqual    => "<=",
            ExpressionType.GreaterThan        => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            _ => throw new NotSupportedException($"Comparison operator {op} not supported")
        };

        private string RenderOperand(Expression expr)
        {
            while (expr is UnaryExpression { NodeType: ExpressionType.Convert } cu)
            {
                expr = cu.Operand;
            }

            if (expr is MemberExpression me && IsParameterChain(me))
            {
                return ResolveField(me);
            }
            if (TryEvaluate(expr, out var val))
            {
                return _bag.Add(val);
            }
            throw NotSupported(expr, "operand must be a parameter member access or a closed-over value");
        }

        private string VisitMethodCall(MethodCallExpression m)
        {
            var method = m.Method;
            var declType = method.DeclaringType;

            if (declType == typeof(string) && m.Object is MemberExpression sObj && IsParameterChain(sObj)
                && m.Arguments.Count >= 1 && TryEvaluate(m.Arguments[0], out var argVal))
            {
                var field = ResolveField(sObj);
                var p = _bag.Add(argVal);
                return method.Name switch
                {
                    nameof(string.Contains)   => $"string::contains({field}, {p})",
                    nameof(string.StartsWith) => $"string::starts_with({field}, {p})",
                    nameof(string.EndsWith)   => $"string::ends_with({field}, {p})",
                    _ => throw NotSupported(m, $"string method {method.Name} not supported")
                };
            }

            if (method.Name == nameof(string.IsNullOrEmpty) && declType == typeof(string)
                && m.Arguments.Count == 1 && m.Arguments[0] is MemberExpression sme && IsParameterChain(sme))
            {
                var field = ResolveField(sme);
                var emptyP = _bag.Add(string.Empty);
                return $"({field} IS NONE OR {field} = {emptyP})";
            }

            if (method.Name == "Contains")
            {
                // Enumerable.Contains<T>(IEnumerable<T>, T)
                if (m.Object is null && m.Arguments.Count == 2)
                {
                    return RenderContains(m.Arguments[0], m.Arguments[1], m);
                }
                // ICollection<T>.Contains(T)
                if (m.Object is not null && m.Arguments.Count == 1 && IsEnumerable(m.Object.Type))
                {
                    return RenderContains(m.Object, m.Arguments[0], m);
                }
            }

            throw NotSupported(m, $"method {declType?.Name}.{method.Name} not supported");
        }

        private string RenderContains(Expression source, Expression item, Expression origin)
        {
            var sourceIsParam = source is MemberExpression sme && IsParameterChain(sme);
            var itemIsParam   = item is MemberExpression ime && IsParameterChain(ime);

            if (sourceIsParam && !itemIsParam && TryEvaluate(item, out var itemVal))
            {
                var field = ResolveField((MemberExpression)source);
                return $"{field} CONTAINS {_bag.Add(itemVal)}";
            }
            if (!sourceIsParam && itemIsParam && TryEvaluate(source, out var srcVal))
            {
                var field = ResolveField((MemberExpression)item);
                return $"{field} IN {_bag.Add(srcVal)}";
            }
            throw NotSupported(origin, "Contains needs exactly one parameter-bound side and one closed-over value");
        }

        private static bool IsEnumerable(Type t) =>
            t != typeof(string) && (typeof(IEnumerable).IsAssignableFrom(t));

        private bool IsParameterChain(MemberExpression me)
        {
            Expression? current = me;
            while (current is MemberExpression mm)
            {
                current = mm.Expression;
            }
            return current is ParameterExpression p && _params.Contains(p);
        }

        private bool ContainsParameter(Expression e) => new ParameterDetector(_params).Find(e);

        private static string ResolveField(MemberExpression me)
        {
            var parts = new Stack<string>();
            Expression? current = me;
            while (current is MemberExpression mm)
            {
                parts.Push(MemberNameResolver.Resolve(mm.Member));
                current = mm.Expression;
            }
            return string.Join(".", parts);
        }

        private bool TryEvaluate(Expression e, out object? value)
        {
            if (ContainsParameter(e))
            {
                value = null;
                return false;
            }
            try
            {
                if (e is ConstantExpression c)
                {
                    value = c.Value;
                    return true;
                }
                var lambda = Expression.Lambda(Expression.Convert(e, typeof(object)));
                value = lambda.Compile().DynamicInvoke();
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private static NotSupportedException NotSupported(Expression e, string detail) =>
            new($"ExpressionToWhere: cannot translate '{e}' ({detail}). Drop down to .WhereRaw(...) for unsupported shapes.");
    }

    private sealed class ParameterDetector : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _params;
        private bool _found;

        public ParameterDetector(HashSet<ParameterExpression> @params) => _params = @params;

        public bool Find(Expression e)
        {
            _found = false;
            Visit(e);
            return _found;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_params.Contains(node))
            {
                _found = true;
            }
            return node;
        }
    }
}
