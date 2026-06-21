namespace SurrealDb.Net.Linq;

/// <summary>(SQL, parameters) pair ready for execution against SurrealDB.</summary>
public interface ISurrealCommand
{
    string Sql { get; }

    IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// Placeholders (sin el prefijo <c>$</c>) en orden de aparición dentro de
    /// <see cref="Sql"/>. Permite a <see cref="SurrealTransactionBuilder"/>
    /// reescribir nombres de parámetros sin parsear el SQL. Default: vacío
    /// (comportamiento legacy para comandos construidos fuera de los builders,
    /// p.e. <see cref="SurrealQuery.Raw(string, IDictionary{string, object?}?)"/>
    /// o <see cref="SurrealQuery.Kill"/>).
    /// </summary>
    IReadOnlyList<string> Placeholders { get; }
}
