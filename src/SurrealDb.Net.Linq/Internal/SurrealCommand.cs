namespace SurrealDb.Net.Linq;

internal sealed record SurrealCommand(string Sql, IReadOnlyDictionary<string, object?> Parameters)
    : ISurrealCommand;
