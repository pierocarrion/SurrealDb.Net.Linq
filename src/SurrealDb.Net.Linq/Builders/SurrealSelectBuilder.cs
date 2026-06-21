using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Fluent builder for <c>SELECT … FROM &lt;target&gt;</c> and <c>LIVE SELECT … FROM &lt;target&gt;</c>.
/// Chain calls in the order they read in SurrealQL: projection → from
/// (already set) → where → group by → order by → limit → fetch.
/// </summary>
public sealed class SurrealSelectBuilder
{
    private readonly string _target;
    private readonly bool _isLive;
    private readonly ParameterBag _bag = new();
    private readonly WhereClauseBuilder _where;

    private readonly List<string> _projection = new();
    private string? _selectValueExpr;
    private bool _onlyRecord;

    private readonly List<(string field, SortDirection dir)> _orderBy = new();
    private readonly List<string> _groupBy = new();
    private readonly List<string> _fetch = new();
    private int? _limit;
    private int? _start;

    internal SurrealSelectBuilder(string target, bool isLive)
    {
        Arg.NotNullOrWhiteSpace(target);
        _target = target;
        _isLive = isLive;
        _where = new WhereClauseBuilder(_bag);
    }

    internal ParameterBag Bag => _bag;
    internal void AddRawWhere(string fragment, string conjunction) => _where.AddRaw(fragment, conjunction);

    /// <summary>Project explicit columns. Replaces any prior <see cref="Field"/>/<see cref="Select"/>/<see cref="SelectValue"/> calls.</summary>
    public SurrealSelectBuilder Select(params string[] fields)
    {
        _projection.Clear();
        _projection.AddRange(fields);
        _selectValueExpr = null;
        return this;
    }

    /// <summary>Append one column to the projection.</summary>
    public SurrealSelectBuilder Field(string name)
    {
        Arg.NotNullOrWhiteSpace(name);
        _projection.Add(name);
        _selectValueExpr = null;
        return this;
    }

    /// <summary>Replace projection with <c>SELECT VALUE &lt;expr&gt;</c> (returns a flat value, not a row).</summary>
    public SurrealSelectBuilder SelectValue(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _projection.Clear();
        _selectValueExpr = expression;
        return this;
    }

    /// <summary>Marks the FROM target as a single record (<c>SELECT * FROM ONLY &lt;record&gt;</c>) — returns one row, not a list.</summary>
    public SurrealSelectBuilder Only()
    {
        _onlyRecord = true;
        return this;
    }

    public SurrealSelectBuilder Where(string field, SurrealOperator op, object? value = null)
    {
        Arg.NotNullOrWhiteSpace(field);
        _where.Add(field, op, value, conjunction: "AND");
        return this;
    }

    public SurrealSelectBuilder And(string field, SurrealOperator op, object? value = null) =>
        Where(field, op, value);

    public SurrealSelectBuilder Or(string field, SurrealOperator op, object? value = null)
    {
        _where.Add(field, op, value, conjunction: "OR");
        return this;
    }

    /// <summary>
    /// Escape hatch for filter shapes the typed methods don't cover (graph
    /// traversals, function calls). Provide the raw expression and the
    /// parameter bindings; placeholders should already be embedded.
    /// </summary>
    public SurrealSelectBuilder WhereRaw(string expression, IDictionary<string, object?>? parameters = null)
    {
        Arg.NotNullOrWhiteSpace(expression);
        if (parameters is not null)
        {
            foreach (var kv in parameters)
            {
                _bag.AddNamed(kv.Key, kv.Value);
            }
        }
        _where.AddRaw(expression, conjunction: "AND");
        return this;
    }

    public SurrealSelectBuilder OrderBy(string field, SortDirection dir = SortDirection.Asc)
    {
        Arg.NotNullOrWhiteSpace(field);
        _orderBy.Add((field, dir));
        return this;
    }

    public SurrealSelectBuilder GroupBy(params string[] fields)
    {
        if (fields is null) throw new ArgumentNullException(nameof(fields));
        _groupBy.AddRange(fields);
        return this;
    }

    public SurrealSelectBuilder Limit(int n)
    {
        Arg.NonNegative(n);
        _limit = n;
        return this;
    }

    public SurrealSelectBuilder Start(int n)
    {
        Arg.NonNegative(n);
        _start = n;
        return this;
    }

    /// <summary>Add a <c>FETCH &lt;field&gt;[, …]</c> clause to dereference record links inline.</summary>
    public SurrealSelectBuilder Fetch(params string[] fields)
    {
        if (fields is null) throw new ArgumentNullException(nameof(fields));
        _fetch.AddRange(fields);
        return this;
    }

    public ISurrealCommand Build()
    {
        var sb = new StringBuilder();
        if (_isLive)
        {
            sb.Append("LIVE ");
        }
        sb.Append("SELECT ");

        if (!string.IsNullOrEmpty(_selectValueExpr))
        {
            sb.Append("VALUE ").Append(_selectValueExpr);
        }
        else if (_projection.Count == 0)
        {
            sb.Append('*');
        }
        else
        {
            sb.Append(string.Join(", ", _projection));
        }

        sb.Append(" FROM ");
        if (_onlyRecord) sb.Append("ONLY ");
        sb.Append(_target);

        if (_where.HasClause)
        {
            sb.Append(" WHERE ").Append(_where.Render());
        }

        if (_groupBy.Count > 0)
        {
            sb.Append(" GROUP BY ").Append(string.Join(", ", _groupBy));
        }

        if (_orderBy.Count > 0)
        {
            sb.Append(" ORDER BY ");
            for (var i = 0; i < _orderBy.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var (f, d) = _orderBy[i];
                sb.Append(f).Append(' ').Append(d == SortDirection.Asc ? "ASC" : "DESC");
            }
        }

        if (_limit is { } limit) sb.Append(" LIMIT ").Append(limit);
        if (_start is { } start) sb.Append(" START ").Append(start);

        if (_fetch.Count > 0)
        {
            sb.Append(" FETCH ").Append(string.Join(", ", _fetch));
        }

        return new SurrealCommand(sb.ToString(), _bag.Snapshot(), _bag.GetPlaceholders());
    }
}
