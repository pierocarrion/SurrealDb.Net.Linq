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
    private string? _contentExpr;
    private string? _mergeExpr;
    private string? _patchExpr;

    internal SurrealCreateBuilder(string target)
    {
        Arg.NotNullOrWhiteSpace(target);
        _target = target;
    }

    /// <summary>Set a field to a parameterized value.</summary>
    public SurrealCreateBuilder Set(string field, object? value)
    {
        Arg.NotNullOrWhiteSpace(field);
        var placeholder = _bag.Add(value);
        _setClauses.Add($"{field} = {placeholder}");
        return this;
    }

    /// <summary>Set a field to a raw SurrealQL expression (e.g. <c>time::now()</c>, <c>type::record($id)</c>).</summary>
    public SurrealCreateBuilder SetExpr(string field, string expression)
    {
        Arg.NotNullOrWhiteSpace(field);
        Arg.NotNullOrWhiteSpace(expression);
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
        Arg.NotNullOrWhiteSpace(name);
        _bag.AddNamed(name, value);
        return this;
    }

    /// <summary>
    /// Reemplaza SET con <c>CONTENT $expr</c> — el cuerpo entero del row
    /// viene dado por un objeto serializado. Mutuamente excluyente con
    /// <see cref="Set"/>/<see cref="Merge"/>/<see cref="Patch"/>.
    /// </summary>
    public SurrealCreateBuilder Content(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _contentExpr = expression;
        return this;
    }

    /// <summary>
    /// Reemplaza SET con <c>MERGE $expr</c> — mergea el objeto dado sobre
    /// cualquier contenido existente.
    /// </summary>
    public SurrealCreateBuilder Merge(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _mergeExpr = expression;
        return this;
    }

    /// <summary>
    /// Reemplaza SET con <c>PATCH $expr</c> — aplica un JSON Patch (RFC 6902)
    /// al contenido existente.
    /// </summary>
    public SurrealCreateBuilder Patch(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _patchExpr = expression;
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
    public SurrealCreateBuilder Return(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _returnClause = expression;
        return this;
    }

    /// <summary>
    /// Set RETURN mode using <see cref="SurrealReturn"/> enum. Replaces the
    /// literal-string overloads (<c>ReturnBefore</c> / <c>ReturnAfter</c> /
    /// <c>ReturnNone</c> / <c>ReturnDiff</c>) when the call site already has
    /// the value as enum.
    /// </summary>
    public SurrealCreateBuilder Return(SurrealReturn value)
    {
        _returnClause = SurrealReturnRenderer.Render(value);
        return this;
    }

    /// <summary><c>RETURN field1, field2, …</c> — pick specific fields by name.</summary>
    public SurrealCreateBuilder ReturnFields(params string[] fields)
    {
        if (fields is null || fields.Length == 0)
            throw new ArgumentException("ReturnFields requires at least one field.", nameof(fields));
        foreach (var f in fields) Arg.NotNullOrWhiteSpace(f);
        _returnClause = string.Join(", ", fields);
        return this;
    }

    public ISurrealCommand Build()
    {
        var hasBody = _setClauses.Count > 0
            || _contentExpr is not null
            || _mergeExpr is not null
            || _patchExpr is not null;
        if (!hasBody)
        {
            throw new InvalidOperationException("CREATE requires at least one Set/SetExpr/Content/Merge/Patch clause.");
        }
        if (new[] { _setClauses.Count > 0, _contentExpr is not null, _mergeExpr is not null, _patchExpr is not null }
            .Count(b => b) > 1)
        {
            throw new InvalidOperationException("CREATE: SET, CONTENT, MERGE and PATCH are mutually exclusive.");
        }
        var sb = new StringBuilder();
        sb.Append("CREATE ").Append(_target);
        if (_setClauses.Count > 0)
        {
            sb.Append(" SET ").Append(string.Join(", ", _setClauses));
        }
        else if (_contentExpr is not null)
        {
            sb.Append(" CONTENT ").Append(_contentExpr);
        }
        else if (_mergeExpr is not null)
        {
            sb.Append(" MERGE ").Append(_mergeExpr);
        }
        else if (_patchExpr is not null)
        {
            sb.Append(" PATCH ").Append(_patchExpr);
        }
        if (_returnClause is not null) sb.Append(" RETURN ").Append(_returnClause);
        return new SurrealCommand(sb.ToString(), _bag.Snapshot(), _bag.GetPlaceholders());
    }
}
