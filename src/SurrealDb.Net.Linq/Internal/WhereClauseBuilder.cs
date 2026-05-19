using System.Text;

namespace SurrealDb.Net.Linq;

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
