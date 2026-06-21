using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Fluent builder for <c>UPDATE &lt;target&gt; SET … WHERE …</c> and
/// <c>UPSERT &lt;target&gt; SET …</c>. Same SET semantics as
/// <see cref="SurrealCreateBuilder"/> plus an optional WHERE filter.
/// </summary>
public sealed class SurrealUpdateBuilder
{
    private readonly string _target;
    private readonly bool _upsert;
    private readonly ParameterBag _bag = new();
    private readonly List<string> _setClauses = new();
    private readonly WhereClauseBuilder _where;
    private string? _returnClause;
    private string? _contentExpr;
    private string? _mergeExpr;
    private string? _patchExpr;

    internal ParameterBag Bag => _bag;
    internal void AddRawWhere(string fragment, string conjunction) => _where.AddRaw(fragment, conjunction);

    /// <summary>
    /// Bind a record id (or any object) as the target — CBOR handles the
    /// canonical encoding. Use this for record-id targets so the SDK never
    /// has to guess between <c>user:abc</c> and <c>user:⟨abc⟩</c> string
    /// representations.
    /// </summary>
    internal SurrealUpdateBuilder(object recordTarget, bool upsert)
    {
        var placeholder = _bag.Add(recordTarget);
        _target = placeholder;
        _upsert = upsert;
        _where = new WhereClauseBuilder(_bag);
    }

    internal SurrealUpdateBuilder(string target, bool upsert)
    {
        Arg.NotNullOrWhiteSpace(target);
        _target = target;
        _upsert = upsert;
        _where = new WhereClauseBuilder(_bag);
    }

    public SurrealUpdateBuilder Set(string field, object? value)
    {
        Arg.NotNullOrWhiteSpace(field);
        var placeholder = _bag.Add(value);
        _setClauses.Add($"{field} = {placeholder}");
        return this;
    }

    public SurrealUpdateBuilder SetExpr(string field, string expression)
    {
        Arg.NotNullOrWhiteSpace(field);
        Arg.NotNullOrWhiteSpace(expression);
        _setClauses.Add($"{field} = {expression}");
        return this;
    }

    public SurrealUpdateBuilder SetIfPresent(string field, object? value)
    {
        if (value is null) return this;
        if (value is string s && string.IsNullOrEmpty(s)) return this;
        return Set(field, value);
    }

    public SurrealUpdateBuilder Where(string field, SurrealOperator op, object? value = null)
    {
        Arg.NotNullOrWhiteSpace(field);
        _where.Add(field, op, value, conjunction: "AND");
        return this;
    }

    public SurrealUpdateBuilder And(string field, SurrealOperator op, object? value = null) => Where(field, op, value);

    public SurrealUpdateBuilder Or(string field, SurrealOperator op, object? value = null)
    {
        Arg.NotNullOrWhiteSpace(field);
        _where.Add(field, op, value, conjunction: "OR");
        return this;
    }

    public SurrealUpdateBuilder WhereRaw(string expression, IDictionary<string, object?>? parameters = null)
    {
        Arg.NotNullOrWhiteSpace(expression);
        if (parameters is not null)
        {
            foreach (var kv in parameters) _bag.AddNamed(kv.Key, kv.Value);
        }
        _where.AddRaw(expression, conjunction: "AND");
        return this;
    }

    public SurrealUpdateBuilder Bind(string name, object? value)
    {
        Arg.NotNullOrWhiteSpace(name);
        _bag.AddNamed(name, value);
        return this;
    }

    /// <summary><c>CONTENT $expr</c> — full body replacement.</summary>
    public SurrealUpdateBuilder Content(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _contentExpr = expression;
        return this;
    }

    /// <summary><c>MERGE $expr</c> — merge given object onto existing content.</summary>
    public SurrealUpdateBuilder Merge(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _mergeExpr = expression;
        return this;
    }

    /// <summary><c>PATCH $expr</c> — apply JSON Patch (RFC 6902).</summary>
    public SurrealUpdateBuilder Patch(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _patchExpr = expression;
        return this;
    }

    public SurrealUpdateBuilder ReturnAfter() { _returnClause = "AFTER"; return this; }
    public SurrealUpdateBuilder ReturnBefore() { _returnClause = "BEFORE"; return this; }
    public SurrealUpdateBuilder ReturnNone() { _returnClause = "NONE"; return this; }
    public SurrealUpdateBuilder ReturnDiff() { _returnClause = "DIFF"; return this; }
    public SurrealUpdateBuilder Return(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _returnClause = expression;
        return this;
    }

    /// <summary>Set RETURN mode using <see cref="SurrealReturn"/> enum.</summary>
    public SurrealUpdateBuilder Return(SurrealReturn value)
    {
        _returnClause = SurrealReturnRenderer.Render(value);
        return this;
    }

    /// <summary><c>RETURN field1, field2, …</c> — pick specific fields by name.</summary>
    public SurrealUpdateBuilder ReturnFields(params string[] fields)
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
            throw new InvalidOperationException("UPDATE/UPSERT requires at least one Set/SetExpr/Content/Merge/Patch clause.");
        }
        if (new[] { _setClauses.Count > 0, _contentExpr is not null, _mergeExpr is not null, _patchExpr is not null }
            .Count(b => b) > 1)
        {
            throw new InvalidOperationException("UPDATE/UPSERT: SET, CONTENT, MERGE and PATCH are mutually exclusive.");
        }
        var sb = new StringBuilder();
        sb.Append(_upsert ? "UPSERT " : "UPDATE ").Append(_target);
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
        if (_where.HasClause) sb.Append(" WHERE ").Append(_where.Render());
        if (_returnClause is not null) sb.Append(" RETURN ").Append(_returnClause);
        return new SurrealCommand(sb.ToString(), _bag.Snapshot(), _bag.GetPlaceholders());
    }
}
