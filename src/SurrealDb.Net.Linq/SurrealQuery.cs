using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Fluent builder for SurrealQL statements. Repositories never concatenate
/// SurrealQL strings by hand: every value gets a named parameter
/// (<c>$p0</c>, <c>$p1</c>…), reserved words and operators are emitted by the
/// builder, and the builder's <c>Build()</c> method returns the
/// (sql, parameters) pair that <c>ISurrealDbClient.RawQuery</c> expects.
///
/// The surface is the EF-Core-shaped subset most apps actually use:
/// SELECT / LIVE SELECT, CREATE, UPDATE, UPSERT, DELETE, KILL, plus a
/// <see cref="Raw"/> escape hatch for anything exotic (graph traversals,
/// <c>math::*</c>, transactions, etc.).
/// </summary>
public static class SurrealQuery
{
    /// <summary>Begin a <c>SELECT … FROM &lt;target&gt;</c> statement.</summary>
    public static SurrealSelectBuilder From(string target) => new(target, isLive: false);

    /// <summary>Begin a <c>LIVE SELECT … FROM &lt;target&gt;</c> statement.</summary>
    public static SurrealSelectBuilder Live(string target) => new(target, isLive: true);

    /// <summary>Begin a <c>CREATE &lt;target&gt;</c> statement.</summary>
    public static SurrealCreateBuilder Create(string target) => new(target);

    /// <summary>Begin an <c>UPDATE &lt;target&gt;</c> statement against a literal target string.</summary>
    public static SurrealUpdateBuilder Update(string target) => new(target, upsert: false);

    /// <summary>
    /// Begin an <c>UPDATE</c> against a record id passed as a CBOR parameter — the
    /// preferred form for typed callers. Avoids the <c>RecordId.ToString()</c>
    /// round-trip mismatch where a record id like <c>user:01HZ…</c> can be
    /// rendered with or without angle brackets and silently match no rows.
    /// </summary>
    public static SurrealUpdateBuilder UpdateRecord(object recordId) => new(recordId, upsert: false);

    /// <summary>Begin an <c>UPSERT &lt;target&gt;</c> statement against a literal target string.</summary>
    public static SurrealUpdateBuilder Upsert(string target) => new(target, upsert: true);

    /// <summary><c>UPSERT</c> against a record id passed as a CBOR parameter.</summary>
    public static SurrealUpdateBuilder UpsertRecord(object recordId) => new(recordId, upsert: true);

    /// <summary>Begin a <c>DELETE &lt;target&gt;</c> statement.</summary>
    public static SurrealDeleteBuilder Delete(string target) => new(target);

    /// <summary>Build a <c>KILL $live</c> statement.</summary>
    public static ISurrealCommand Kill(Guid liveId) =>
        new SurrealCommand("KILL $live", new Dictionary<string, object?> { ["live"] = liveId });

    /// <summary>Wrap a hand-written SurrealQL string. Use sparingly — prefer the typed builders.</summary>
    public static ISurrealCommand Raw(string sql, IDictionary<string, object?>? parameters = null) =>
        new SurrealCommand(sql, parameters is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(parameters));
}

/// <summary>(SQL, parameters) pair ready for execution against SurrealDB.</summary>
public interface ISurrealCommand
{
    string Sql { get; }
    IReadOnlyDictionary<string, object?> Parameters { get; }
}

internal sealed record SurrealCommand(string Sql, IReadOnlyDictionary<string, object?> Parameters)
    : ISurrealCommand;

/// <summary>Comparison operators allowed in <c>Where</c>/<c>And</c>/<c>Or</c> clauses.</summary>
public enum SurrealOperator
{
    Equals,
    NotEquals,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    In,
    NotIn,
    Contains,
    ContainsNot,
    Inside,
    Like,
    IsNone,
    IsNotNone,
}

/// <summary>Sort direction for <c>ORDER BY</c>.</summary>
public enum SortDirection { Asc, Desc }

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
        SurrealOperator.Like            => $"string::contains({field}, {placeholder})",
        SurrealOperator.IsNone          => $"{field} IS NONE",
        SurrealOperator.IsNotNone       => $"{field} IS NOT NONE",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unsupported operator"),
    };

    /// <summary>True when the operator takes a right-hand parameter (not unary IS NONE / IS NOT NONE).</summary>
    public static bool IsBinary(SurrealOperator op) =>
        op != SurrealOperator.IsNone && op != SurrealOperator.IsNotNone;
}

/// <summary>Common parameter book-keeping shared by every builder. Names auto-generate as <c>p0, p1, …</c>.</summary>
internal sealed class ParameterBag
{
    private readonly Dictionary<string, object?> _params = new();
    private int _next;

    public string Add(object? value)
    {
        var name = $"p{_next++}";
        _params[name] = value;
        return $"${name}";
    }

    public void AddNamed(string name, object? value) => _params[name] = value;

    public IReadOnlyDictionary<string, object?> Snapshot() => _params;
}

/// <summary>Mixin shared by SELECT and DELETE — both expose a <c>WHERE</c> chain with AND/OR composition.</summary>
internal sealed class WhereClauseBuilder
{
    private readonly ParameterBag _bag;
    private readonly StringBuilder _sb = new();
    private bool _hasFirst;

    public WhereClauseBuilder(ParameterBag bag) => _bag = bag;

    public bool HasClause => _hasFirst;

    public void Add(string field, SurrealOperator op, object? value, string conjunction)
    {
        if (_hasFirst)
        {
            _sb.Append(' ').Append(conjunction).Append(' ');
        }
        if (SurrealOperatorRenderer.IsBinary(op))
        {
            var placeholder = _bag.Add(value);
            _sb.Append(SurrealOperatorRenderer.Render(field, op, placeholder));
        }
        else
        {
            _sb.Append(SurrealOperatorRenderer.Render(field, op, null));
        }
        _hasFirst = true;
    }

    public void AddRaw(string expression, string conjunction)
    {
        if (_hasFirst)
        {
            _sb.Append(' ').Append(conjunction).Append(' ');
        }
        _sb.Append('(').Append(expression).Append(')');
        _hasFirst = true;
    }

    public string Render() => _sb.ToString();
}
