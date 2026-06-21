namespace SurrealDb.Net.Linq;

internal sealed record SurrealCommand(
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters,
    IReadOnlyList<string> Placeholders) : ISurrealCommand
{
    /// <summary>
    /// Constructor legacy para comandos sin info de placeholders (p.e.
    /// <see cref="SurrealQuery.Raw(string, IDictionary{string, object?}?)"/> y
    /// <see cref="SurrealQuery.Kill"/>). Usa <see cref="Array.Empty{T}"/>
    /// como default; <see cref="SurrealTransactionBuilder"/> cae a las keys
    /// de <see cref="Parameters"/> cuando la lista está vacía.
    /// </summary>
    public SurrealCommand(string sql, IReadOnlyDictionary<string, object?> parameters)
        : this(sql, parameters, Array.Empty<string>()) { }
}
