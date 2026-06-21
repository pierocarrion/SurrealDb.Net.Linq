namespace SurrealDb.Net.Linq;

/// <summary>
/// Encapsula la reflexión sobre los tipos internos de error SurrealDb.Net.
/// Antes de introducirse esta clase, dos callers
/// (<see cref="SurrealClientExtensions.ExecuteNoResultAsync"/> y
/// <see cref="SurrealClientExtensions.InsertWithIdAsync"/>) repetían la
/// misma reflexión inline y apuntaban a la propiedad equivocada:
/// <c>RpcErrorResponseContent.Details</c> es un objeto complejo
/// (<c>RpcErrorDetails</c>), no el mensaje de error — el texto vive en
/// <c>RpcErrorResponseContent.Message</c>. Centralizarlo aquí reduce el
/// punto único de falla si SurrealDb.Net renombra la forma en el futuro.
/// </summary>
internal static class SurrealDbErrorExtractor
{
    /// <summary>
    /// Extrae el detalle del primer error. Orden de preferencia:
    /// <list type="number">
    ///   <item><c>Message</c> (string) — donde SurrealDB pone el texto del error.</item>
    ///   <item><c>Details</c> (string) — en <c>SurrealDbErrorResult</c> es string.</item>
    ///   <item><c>ToString()</c> del error.</item>
    /// </list>
    /// Antes de 0.4.0 este método seguía el orden inverso (Details → ToString)
    /// lo que silenciaba el mensaje real y rompía la detección de
    /// "Transaction conflict".
    /// </summary>
    public static string GetFirstErrorDetail(System.Collections.IEnumerable errors)
    {
        object? first = null;
        foreach (var e in errors)
        {
            first = e;
            break;
        }
        if (first is null) return "Unknown error";

        // 1. Message (string property — donde vive "Transaction conflict" y similares)
        var msgProp = first.GetType().GetProperty("Message");
        if (msgProp?.GetValue(first) is string msg && !string.IsNullOrEmpty(msg))
            return msg;

        // 2. Details (en RpcErrorResponseContent es un objeto complejo; en
        //    SurrealDbErrorResult es string). Cubrimos ambos casos.
        var detailsProp = first.GetType().GetProperty("Details");
        if (detailsProp is not null)
        {
            var details = detailsProp.GetValue(first);
            if (details is string s && !string.IsNullOrEmpty(s)) return s;
            var detailStr = details?.ToString();
            if (!string.IsNullOrEmpty(detailStr)) return detailStr!;
        }

        return first.ToString() ?? "Unknown error";
    }

    /// <summary>
    /// Comprueba si el detalle corresponde al mensaje "Transaction conflict"
    /// que SurrealDB v3 emite para conflictos de optimistic-concurrency.
    /// Comparación case-insensitive para tolerar variantes tipográficas del
    /// motor. Mantenida como string-match porque SurrealDB no expone un
    /// código de error estable.
    /// </summary>
    public static bool IsTransactionConflict(string detail) =>
        detail.Contains("Transaction conflict", StringComparison.OrdinalIgnoreCase);
}
