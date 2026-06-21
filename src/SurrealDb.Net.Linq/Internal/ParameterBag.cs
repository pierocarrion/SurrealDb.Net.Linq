namespace SurrealDb.Net.Linq;

/// <summary>Common parameter book-keeping shared by every builder. Names auto-generate as <c>p0, p1, …</c>.</summary>
internal sealed class ParameterBag
{
    private readonly Dictionary<string, object?> _params = new();
    private readonly List<string> _order = new();
    private int _next;

    public string Add(object? value)
    {
        var name = $"p{_next++}";
        _params[name] = value;
        _order.Add(name);
        return $"${name}";
    }

    public void AddNamed(string name, object? value)
    {
        if (!_params.ContainsKey(name)) _order.Add(name);
        _params[name] = value;
    }

    /// <summary>Placeholders (sin <c>$</c>) en orden de inserción. Permite
    /// a la transaction reescribir sin parsear SQL.</summary>
    public IReadOnlyList<string> GetPlaceholders() => _order;

    /// <summary>
    /// Devuelve una <b>copia defensiva</b> del diccionario interno. Antes de
    /// 0.4.0 esto devolvía la referencia viva, con lo que seguir añadiendo
    /// parámetros al bag mutaba snapshots ya entregados a un ISurrealCommand.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Snapshot() => new Dictionary<string, object?>(_params);
}
