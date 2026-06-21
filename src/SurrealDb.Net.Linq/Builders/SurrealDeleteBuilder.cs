using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>Fluent builder for <c>DELETE &lt;target&gt; WHERE … RETURN …</c>.</summary>
public sealed class SurrealDeleteBuilder
{
    private readonly string _target;
    private readonly ParameterBag _bag = new();
    private readonly WhereClauseBuilder _where;
    private string? _returnClause;
    private bool _onlyRecord;
    private int? _limit;
    private int? _start;

    internal SurrealDeleteBuilder(string target)
    {
        Arg.NotNullOrWhiteSpace(target);
        _target = target;
        _where = new WhereClauseBuilder(_bag);
    }

    internal ParameterBag Bag => _bag;
    internal void AddRawWhere(string fragment, string conjunction) => _where.AddRaw(fragment, conjunction);

    /// <summary>
    /// Marks the FROM target as a single record (<c>DELETE ONLY &lt;record&gt;</c>)
    /// — deletes one row, returns it instead of a list.
    /// </summary>
    public SurrealDeleteBuilder Only()
    {
        _onlyRecord = true;
        return this;
    }

    public SurrealDeleteBuilder Where(string field, SurrealOperator op, object? value = null)
    {
        Arg.NotNullOrWhiteSpace(field);
        _where.Add(field, op, value, conjunction: "AND");
        return this;
    }

    public SurrealDeleteBuilder And(string field, SurrealOperator op, object? value = null) => Where(field, op, value);
    public SurrealDeleteBuilder Or(string field, SurrealOperator op, object? value = null)
    {
        Arg.NotNullOrWhiteSpace(field);
        _where.Add(field, op, value, conjunction: "OR");
        return this;
    }

    public SurrealDeleteBuilder WhereRaw(string expression, IDictionary<string, object?>? parameters = null)
    {
        Arg.NotNullOrWhiteSpace(expression);
        if (parameters is not null)
        {
            foreach (var kv in parameters) _bag.AddNamed(kv.Key, kv.Value);
        }
        _where.AddRaw(expression, conjunction: "AND");
        return this;
    }

    /// <summary>Limit the number of rows deleted.</summary>
    public SurrealDeleteBuilder Limit(int n)
    {
        Arg.NonNegative(n);
        _limit = n;
        return this;
    }

    /// <summary>Skip the first <c>n</c> rows when deleting.</summary>
    public SurrealDeleteBuilder Start(int n)
    {
        Arg.NonNegative(n);
        _start = n;
        return this;
    }

    public SurrealDeleteBuilder ReturnBefore() { _returnClause = "BEFORE"; return this; }
    public SurrealDeleteBuilder ReturnAfter() { _returnClause = "AFTER"; return this; }
    public SurrealDeleteBuilder ReturnDiff() { _returnClause = "DIFF"; return this; }
    public SurrealDeleteBuilder ReturnNone() { _returnClause = "NONE"; return this; }
    public SurrealDeleteBuilder Return(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _returnClause = expression;
        return this;
    }

    /// <summary>Set RETURN mode using <see cref="SurrealReturn"/> enum.</summary>
    public SurrealDeleteBuilder Return(SurrealReturn value)
    {
        _returnClause = SurrealReturnRenderer.Render(value);
        return this;
    }

    /// <summary><c>RETURN field1, field2, …</c> — pick specific fields by name.</summary>
    public SurrealDeleteBuilder ReturnFields(params string[] fields)
    {
        if (fields is null || fields.Length == 0)
            throw new ArgumentException("ReturnFields requires at least one field.", nameof(fields));
        foreach (var f in fields) Arg.NotNullOrWhiteSpace(f);
        _returnClause = string.Join(", ", fields);
        return this;
    }

    public ISurrealCommand Build()
    {
        var sb = new StringBuilder();
        sb.Append("DELETE ");
        if (_onlyRecord) sb.Append("ONLY ");
        sb.Append(_target);
        if (_where.HasClause) sb.Append(" WHERE ").Append(_where.Render());
        if (_limit is { } limit) sb.Append(" LIMIT ").Append(limit);
        if (_start is { } start) sb.Append(" START ").Append(start);
        if (_returnClause is not null) sb.Append(" RETURN ").Append(_returnClause);
        return new SurrealCommand(sb.ToString(), _bag.Snapshot(), _bag.GetPlaceholders());
    }
}
