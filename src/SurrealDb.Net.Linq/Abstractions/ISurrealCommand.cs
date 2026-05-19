namespace SurrealDb.Net.Linq;

/// <summary>(SQL, parameters) pair ready for execution against SurrealDB.</summary>
public interface ISurrealCommand
{
    string Sql { get; }
    IReadOnlyDictionary<string, object?> Parameters { get; }
}
