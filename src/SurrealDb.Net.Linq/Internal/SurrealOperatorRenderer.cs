namespace SurrealDb.Net.Linq;

/// <summary>Internal helper: appends the textual operator + (when binary) a placeholder, returning the rendered clause.</summary>
internal static class SurrealOperatorRenderer
{
    public static string Render(string field, SurrealOperator op, string? placeholder) => op switch
    {
        SurrealOperator.Equals          => $"{field} = {placeholder}",
        SurrealOperator.NotEquals       => $"{field} != {placeholder}",
        SurrealOperator.Greater         => $"{field} > {placeholder}",
        SurrealOperator.GreaterOrEqual  => $"{field} >= {placeholder}",
        SurrealOperator.Less            => $"{field} < {placeholder}",
        SurrealOperator.LessOrEqual     => $"{field} <= {placeholder}",
        SurrealOperator.In              => $"{field} IN {placeholder}",
        SurrealOperator.NotIn           => $"{field} NOT IN {placeholder}",
        SurrealOperator.Contains        => $"{field} CONTAINS {placeholder}",
        SurrealOperator.ContainsNot     => $"{field} CONTAINSNOT {placeholder}",
        SurrealOperator.Inside          => $"{field} INSIDE {placeholder}",
        SurrealOperator.Outside         => $"{field} OUTSIDE {placeholder}",
        SurrealOperator.Intersects      => $"{field} INTERSECTS {placeholder}",
        SurrealOperator.AllInside       => $"{field} ALLINSIDE {placeholder}",
        SurrealOperator.AnyInside       => $"{field} ANYINSIDE {placeholder}",
        SurrealOperator.AllOutside      => $"{field} ALLOUTSIDE {placeholder}",
        SurrealOperator.AnyOutside      => $"{field} ANYOUTSIDE {placeholder}",
        SurrealOperator.Matches         => $"{field} ~ {placeholder}",
        SurrealOperator.NotMatches      => $"{field} !~ {placeholder}",
        SurrealOperator.Like            => $"string::contains({field}, {placeholder})",
        SurrealOperator.IsNone          => $"{field} IS NONE",
        SurrealOperator.IsNotNone       => $"{field} IS NOT NONE",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unsupported operator"),
    };

    /// <summary>True when the operator takes a right-hand parameter (not unary IS NONE / IS NOT NONE).</summary>
    public static bool IsBinary(SurrealOperator op) =>
        op != SurrealOperator.IsNone && op != SurrealOperator.IsNotNone;
}
