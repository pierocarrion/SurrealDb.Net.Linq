using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Fluent builder for <c>RELATE &lt;source&gt; -&gt; &lt;edge&gt; -&gt; &lt;target&gt; [ SET … ] [ RETURN … ]</c>.
/// Crea una arista de grafo entre dos nodos. Útil para modelar relaciones
/// N:M con propiedades (p.e. <c>user -&gt; wrote -&gt; article</c> con
/// <c>since</c>, <c>role</c>, etc.).
/// </summary>
public sealed class SurrealRelateBuilder
{
    private readonly ParameterBag _bag = new();
    private readonly List<string> _setClauses = new();
    private string? _returnClause;
    private readonly string _sourceToken;
    private readonly string _edge;
    private readonly string _targetToken;
    private readonly bool _unique;

    internal SurrealRelateBuilder(string source, string edge, string target, bool unique = false)
    {
        Arg.NotNullOrWhiteSpace(source);
        Arg.NotNullOrWhiteSpace(edge);
        Arg.NotNullOrWhiteSpace(target);
        _sourceToken = _bag.Add(source);
        _edge = edge;
        _targetToken = _bag.Add(target);
        _unique = unique;
    }

    /// <summary>Set a field to a parameterized value on the new edge row.</summary>
    public SurrealRelateBuilder Set(string field, object? value)
    {
        Arg.NotNullOrWhiteSpace(field);
        var placeholder = _bag.Add(value);
        _setClauses.Add($"{field} = {placeholder}");
        return this;
    }

    /// <summary>Set a field to a raw SurrealQL expression.</summary>
    public SurrealRelateBuilder SetExpr(string field, string expression)
    {
        Arg.NotNullOrWhiteSpace(field);
        Arg.NotNullOrWhiteSpace(expression);
        _setClauses.Add($"{field} = {expression}");
        return this;
    }

    public SurrealRelateBuilder Bind(string name, object? value)
    {
        Arg.NotNullOrWhiteSpace(name);
        _bag.AddNamed(name, value);
        return this;
    }

    public SurrealRelateBuilder ReturnBefore() { _returnClause = "BEFORE"; return this; }
    public SurrealRelateBuilder ReturnAfter() { _returnClause = "AFTER"; return this; }
    public SurrealRelateBuilder ReturnNone() { _returnClause = "NONE"; return this; }
    public SurrealRelateBuilder ReturnDiff() { _returnClause = "DIFF"; return this; }
    public SurrealRelateBuilder Return(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _returnClause = expression;
        return this;
    }
    public SurrealRelateBuilder Return(SurrealReturn value)
    {
        _returnClause = SurrealReturnRenderer.Render(value);
        return this;
    }

    public ISurrealCommand Build()
    {
        var sb = new StringBuilder();
        sb.Append("RELATE ");
        if (_unique) sb.Append("UNIQUE ");
        sb.Append(_sourceToken)
          .Append(" -> ").Append(_edge).Append(" -> ")
          .Append(_targetToken);

        if (_setClauses.Count > 0)
        {
            sb.Append(" SET ").Append(string.Join(", ", _setClauses));
        }
        if (_returnClause is not null) sb.Append(" RETURN ").Append(_returnClause);

        return new SurrealCommand(sb.ToString(), _bag.Snapshot(), _bag.GetPlaceholders());
    }
}
