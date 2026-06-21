namespace SurrealDb.Net.Linq;

internal static class SurrealReturnRenderer
{
    public static string Render(SurrealReturn value) => value switch
    {
        SurrealReturn.None => "NONE",
        SurrealReturn.Before => "BEFORE",
        SurrealReturn.After => "AFTER",
        SurrealReturn.Diff => "DIFF",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown SurrealReturn value."),
    };
}
