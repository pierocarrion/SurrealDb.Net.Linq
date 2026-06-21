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
                case MemberExpression me when IsNullableHasValueOrValue(me):
                    throw NotSupported(me,
                        $"Nullable<{GetNullableUnderlyingType(me.Expression!.Type)?.Name}>.{me.Member.Name} " +
                        "is not supported — use the underlying member directly (e.g. u.Age != null).");
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

        private static bool IsNullableHasValueOrValue(MemberExpression me)
        {
            if (me.Member.Name is not ("HasValue" or "Value")) return false;
            if (me.Expression is not MemberExpression inner) return false;
            return IsNullableOfT(inner.Type);
        }

        private static Type? GetNullableUnderlyingType(Type nullableType) =>
            IsNullableOfT(nullableType) ? Nullable.GetUnderlyingType(nullableType) : null;

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

            // MethodCallExpression anidado como operand (e.g.
            // r.Email.Trim() == "x") — delega al dispatcher de methods SOLO
            // si toca un parameter del lambda; si es un closure puro
            // (GetEmail() helper method), cae a TryEvaluate como antes.
            if (expr is MethodCallExpression mc && ContainsParameter(mc))
            {
                return VisitMethodCall(mc);
            }

            if (expr is MemberExpression me && IsParameterChain(me))
            {
                // DateTime/DateTimeOffset property access → time::* function calls.
                // Sin esto, u.CreatedAt.Year se renderiza como "created_at.year"
                // (campo inexistente) en vez de "time::year(created_at)".
                if (TryRenderDateTimeMember(me, out var dtFuncCall) && dtFuncCall is not null)
                {
                    return dtFuncCall;
                }
                // string.Length → string::len(field)
                if (me.Member.Name == nameof(string.Length) && me.Expression is MemberExpression inner
                    && IsParameterChain(inner) && inner.Type == typeof(string))
                {
                    return $"string::len({ResolveField(inner)})";
                }
                return ResolveField(me);
            }
            if (TryEvaluate(expr, out var val))
            {
                return _bag.Add(val);
            }
            throw NotSupported(expr, "operand must be a parameter member access or a closed-over value");
        }

        private static bool IsNullableOfT(Type? t) =>
            t is not null && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);

        /// <summary>
        /// Si <paramref name="me"/> es <c>u.CreatedAt.Year</c> (o cualquier
        /// propiedad de fecha/hora), renderiza como <c>time::year(created_at)</c>.
        /// Tabla de mapeo:
        /// <list type="bullet">
        ///   <item><c>DateTime.Year</c> → <c>time::year</c></item>
        ///   <item><c>DateTime.Month</c> → <c>time::month</c></item>
        ///   <item><c>DateTime.Day</c> → <c>time::day</c></item>
        ///   <item><c>DateTime.Hour</c> → <c>time::hour</c></item>
        ///   <item><c>DateTime.Minute</c> → <c>time::minute</c></item>
        ///   <item><c>DateTime.Second</c> → <c>time::second</c></item>
        ///   <item><c>DateTime.DayOfWeek</c> → no match directo; lanzar</item>
        ///   <item><c>DateTime.Date</c> → <c>datetime::date</c></item>
        /// </list>
        /// </summary>
        private bool TryRenderDateTimeMember(MemberExpression me, out string? result)
        {
            result = null;
            if (me.Expression is not MemberExpression inner) return false;
            var innerType = inner.Type;
            if (innerType != typeof(DateTime) && innerType != typeof(DateTimeOffset)) return false;
            if (!IsParameterChain(inner)) return false;

            var field = ResolveField(inner);
            var fn = me.Member.Name switch
            {
                nameof(DateTime.Year) => "time::year",
                nameof(DateTime.Month) => "time::month",
                nameof(DateTime.Day) => "time::day",
                nameof(DateTime.Hour) => "time::hour",
                nameof(DateTime.Minute) => "time::minute",
                nameof(DateTime.Second) => "time::second",
                nameof(DateTime.Date) => "datetime::date",
                nameof(DateTime.DayOfWeek) => "time::wday",
                nameof(DateTime.DayOfYear) => "time::yday",
                _ => null,
            };
            if (fn is null) return false;
            result = $"{fn}({field})";
            return true;
        }

        private string VisitMethodCall(MethodCallExpression m)
        {
            var method = m.Method;
            var declType = method.DeclaringType;

            if (declType == typeof(string) && m.Object is MemberExpression sObj && IsParameterChain(sObj))
            {
                var field = ResolveField(sObj);

                // No-args methods: ToLower, ToUpper, Trim.
                if (m.Arguments.Count == 0)
                {
                    var noArgResult = method.Name switch
                    {
                        nameof(string.ToLower) => $"string::lowercase({field})",
                        nameof(string.ToUpper) => $"string::uppercase({field})",
                        nameof(string.Trim)    => $"string::trim({field})",
                        _ => null,
                    };
                    if (noArgResult is not null) return noArgResult;
                }

                // Single-arg methods: Contains, StartsWith, EndsWith.
                if (m.Arguments.Count >= 1 && TryEvaluate(m.Arguments[0], out var argVal))
                {
                    // Note: no _bag.Add side-effect here — el binding va dentro
                    // del switch para no "quemar" placeholders cuando el method
                    // no matchea (Replace cae al siguiente branch).
                    var oneArgResult = method.Name switch
                    {
                        nameof(string.Contains)   => $"string::contains({field}, {_bag.Add(argVal)})",
                        nameof(string.StartsWith) => $"string::starts_with({field}, {_bag.Add(argVal)})",
                        nameof(string.EndsWith)   => $"string::ends_with({field}, {_bag.Add(argVal)})",
                        _ => null,
                    };
                    if (oneArgResult is not null) return oneArgResult;
                }

                // Two-arg Replace(field, old, new).
                if (method.Name == nameof(string.Replace) && m.Arguments.Count == 2
                    && TryEvaluate(m.Arguments[0], out var repOld) && TryEvaluate(m.Arguments[1], out var repNew))
                {
                    return $"string::replace({field}, {_bag.Add(repOld)}, {_bag.Add(repNew)})";
                }
            }

            if (method.Name == nameof(string.IsNullOrEmpty) && declType == typeof(string)
                && m.Arguments.Count == 1 && m.Arguments[0] is MemberExpression sme && IsParameterChain(sme))
            {
                var field = ResolveField(sme);
                var emptyP = _bag.Add(string.Empty);
                return $"({field} IS NONE OR {field} = {emptyP})";
            }

            if (method.Name == nameof(string.IsNullOrWhiteSpace) && declType == typeof(string)
                && m.Arguments.Count == 1 && m.Arguments[0] is MemberExpression swe && IsParameterChain(swe))
            {
                var field = ResolveField(swe);
                var emptyP = _bag.Add(string.Empty);
                return $"({field} IS NONE OR {field} = {emptyP} OR string::trim({field}) = {emptyP})";
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
