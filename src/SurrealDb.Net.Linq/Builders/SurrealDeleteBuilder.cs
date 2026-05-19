using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>Fluent builder for <c>DELETE &lt;target&gt; WHERE … RETURN …</c>.</summary>
public sealed class SurrealDeleteBuilder
{
    private readonly string _target;
    private readonly ParameterBag _bag = new();
    private readonly WhereClauseBuilder _where;
    private string? _returnClause;

    internal SurrealDeleteBuilder(string target)
    {
        _target = target;
        _where = new WhereClauseBuilder(_bag);
    }

    internal ParameterBag Bag => _bag;
    internal void AddRawWhere(string fragment, string conjunction) => _where.AddRaw(fragment, conjunction);

    public SurrealDeleteBuilder Where(string field, SurrealOperator op, object? value = null)
    {
        _where.Add(field, op, value, conjunction: "AND");
        return this;
    }

    public SurrealDeleteBuilder And(string field, SurrealOperator op, object? value = null) => Where(field, op, value);
    public SurrealDeleteBuilder Or(string field, SurrealOperator op, object? value = null)
    {
        _where.Add(field, op, value, conjunction: "OR");
        return this;
    }

    public SurrealDeleteBuilder WhereRaw(string expression, IDictionary<string, object?>? parameters = null)
    {
        if (parameters is not null)
        {
            foreach (var kv in parameters) _bag.AddNamed(kv.Key, kv.Value);
        }
        _where.AddRaw(expression, conjunction: "AND");
        return this;
    }

    public SurrealDeleteBuilder ReturnBefore() { _returnClause = "BEFORE"; return this; }
    public SurrealDeleteBuilder ReturnDiff() { _returnClause = "DIFF"; return this; }
    public SurrealDeleteBuilder ReturnNone() { _returnClause = "NONE"; return this; }

    public ISurrealCommand Build()
    {
        var sb = new StringBuilder();
        sb.Append("DELETE ").Append(_target);
        if (_where.HasClause) sb.Append(" WHERE ").Append(_where.Render());
        if (_returnClause is not null) sb.Append(" RETURN ").Append(_returnClause);
        return new SurrealCommand(sb.ToString(), _bag.Snapshot());
    }
}
