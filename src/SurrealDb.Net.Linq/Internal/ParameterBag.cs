namespace SurrealDb.Net.Linq;

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
