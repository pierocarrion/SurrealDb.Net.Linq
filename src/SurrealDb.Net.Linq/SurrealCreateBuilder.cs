using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Fluent builder for <c>CREATE &lt;target&gt; SET … RETURN …</c>. Use
/// <see cref="Set"/> for a value (parameterized), <see cref="SetExpr"/> when
/// the right-hand side is a SurrealQL expression like <c>time::now()</c> that
/// must NOT be quoted as a parameter.
/// </summary>
public sealed class SurrealCreateBuilder
{
    private readonly string _target;
    private readonly ParameterBag _bag = new();
    private readonly List<string> _setClauses = new();

    private string? _returnClause;

    internal SurrealCreateBuilder(string target) => _target = target;

    /// <summary>Set a field to a parameterized value.</summary>
    public SurrealCreateBuilder Set(string field, object? value)
    {
        var placeholder = _bag.Add(value);
        _setClauses.Add($"{field} = {placeholder}");
        return this;
    }

    /// <summary>Set a field to a raw SurrealQL expression (e.g. <c>time::now()</c>, <c>type::record($id)</c>).</summary>
    public SurrealCreateBuilder SetExpr(string field, string expression)
    {
        _setClauses.Add($"{field} = {expression}");
        return this;
    }

    /// <summary>Conditional set — only emits when <paramref name="value"/> is non-null and non-empty (string).</summary>
    public SurrealCreateBuilder SetIfPresent(string field, object? value)
    {
        if (value is null) return this;
        if (value is string s && string.IsNullOrEmpty(s)) return this;
        return Set(field, value);
    }

    /// <summary>Bulk parameter binding (without emitting a SET clause). Useful when <see cref="SetExpr"/> embeds custom placeholders.</summary>
    public SurrealCreateBuilder Bind(string name, object? value)
    {
        _bag.AddNamed(name, value);
        return this;
    }

    /// <summary><c>RETURN BEFORE</c>.</summary>
    public SurrealCreateBuilder ReturnBefore() { _returnClause = "BEFORE"; return this; }

    /// <summary><c>RETURN AFTER</c>.</summary>
    public SurrealCreateBuilder ReturnAfter() { _returnClause = "AFTER"; return this; }

    /// <summary><c>RETURN NONE</c>.</summary>
    public SurrealCreateBuilder ReturnNone() { _returnClause = "NONE"; return this; }

    /// <summary><c>RETURN DIFF</c>.</summary>
    public SurrealCreateBuilder ReturnDiff() { _returnClause = "DIFF"; return this; }

    /// <summary><c>RETURN &lt;expr&gt;</c> — pick specific fields.</summary>
    public SurrealCreateBuilder Return(string expression) { _returnClause = expression; return this; }

    public ISurrealCommand Build()
    {
        if (_setClauses.Count == 0)
        {
            throw new InvalidOperationException("CREATE requires at least one Set / SetExpr clause.");
        }
        var sb = new StringBuilder();
        sb.Append("CREATE ").Append(_target).Append(" SET ").Append(string.Join(", ", _setClauses));
        if (_returnClause is not null) sb.Append(" RETURN ").Append(_returnClause);
        return new SurrealCommand(sb.ToString(), _bag.Snapshot());
    }
}
