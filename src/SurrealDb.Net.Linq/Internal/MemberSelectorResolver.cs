using System.Linq.Expressions;
using System.Reflection;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Helpers para extraer nombres de campo SurrealQL desde expressions
/// <c>Expression&lt;Func&lt;T, K&gt;&gt;</c> tipadas en los builders
/// genéricos (<see cref="SurrealCreateBuilder{T}"/>,
/// <see cref="SurrealUpdateBuilder{T}"/>). Soporta cadenas anidadas
/// <c>u.Address.City</c> → <c>address.city</c>.
/// </summary>
internal static class MemberSelectorResolver
{
    /// <summary>
    /// Resuelve una expression <c>x =&gt; x.Foo</c> o <c>x =&gt; x.Foo.Bar</c>
    /// en el nombre de campo SurrealQL equivalente (usando
    /// <see cref="MemberNameResolver"/>). Lanza si la forma no es soportada.
    /// </summary>
    public static string ResolveField<T, K>(Expression<Func<T, K>> selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return ResolveFromExpression(selector.Body);
    }

    private static string ResolveFromExpression(Expression expr)
    {
        // Quitar conversions implícitas (boxing, nullable unwrap).
        while (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert)
            expr = u.Operand;

        if (expr is MemberExpression me)
        {
            var parts = new List<string>();
            Expression? current = me;
            while (current is MemberExpression m)
            {
                parts.Insert(0, MemberNameResolver.Resolve(m.Member));
                current = m.Expression;
            }
            if (current is not ParameterExpression)
            {
                throw new ArgumentException(
                    $"Selector must be a member chain rooted on the row parameter (e.g. x => x.Foo.Bar). " +
                    $"Got: {me}.", nameof(me));
            }
            return string.Join('.', parts);
        }

        throw new ArgumentException(
            $"Selector must be a member access (e.g. x => x.Email). Got: {expr}.", nameof(expr));
    }
}
