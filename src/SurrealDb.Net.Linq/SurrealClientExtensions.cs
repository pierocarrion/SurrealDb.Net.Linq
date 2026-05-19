using SurrealDb.Net.Models;
using SurrealDb.Net.Models.Response;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Bridges <see cref="ISurrealCommand"/> objects (built by the fluent builders
/// in this package) to the underlying <see cref="ISurrealDbClient"/> RPC. Use
/// these extensions instead of calling <c>RawQuery</c> with hand-spliced
/// SurrealQL — the SQL stays in the builder and parameter safety is automatic.
/// </summary>
public static class SurrealClientExtensions
{
    /// <summary>Execute a built command and return the raw <see cref="SurrealDbResponse"/>.</summary>
    public static Task<SurrealDbResponse> ExecuteAsync(
        this ISurrealDbClient client,
        ISurrealCommand command,
        CancellationToken ct = default) =>
        client.RawQuery(command.Sql, command.Parameters, ct);

    /// <summary>Execute and project the first statement's result to <typeparamref name="T"/>; default when missing.</summary>
    public static async Task<T?> ExecuteScalarAsync<T>(
        this ISurrealDbClient client,
        ISurrealCommand command,
        CancellationToken ct = default)
    {
        var response = await client.RawQuery(command.Sql, command.Parameters, ct).ConfigureAwait(false);
        try { return response.GetValue<T>(0); }
        catch { return default; }
    }

    /// <summary>Execute and read the first statement's result as <c>List&lt;T&gt;</c>; empty when nothing.</summary>
    public static async Task<List<T>> ExecuteListAsync<T>(
        this ISurrealDbClient client,
        ISurrealCommand command,
        CancellationToken ct = default)
    {
        var response = await client.RawQuery(command.Sql, command.Parameters, ct).ConfigureAwait(false);
        return response.GetValue<List<T>>(0) ?? new List<T>();
    }

    /// <summary>Execute and discard the result. For UPDATE / DELETE / KILL where you don't care about the rows.</summary>
    /// <remarks>
    /// Surfaces per-statement errors as <see cref="InvalidOperationException"/> so
    /// schema ASSERTs and UNIQUE-index violations don't get silently swallowed by
    /// the underlying <c>RawQuery</c> envelope.
    ///
    /// Exception: SurrealDB v3 "Transaction conflict" responses are part of the
    /// engine's optimistic-concurrency contract ("This transaction can be
    /// retried"). They are not bugs and do not represent business errors — the
    /// loser of a write race sees a clean no-op rather than a 500. The caller
    /// must read state explicitly if it needs to detect that nothing changed.
    /// </remarks>
    public static async Task ExecuteNoResultAsync(
        this ISurrealDbClient client,
        ISurrealCommand command,
        CancellationToken ct = default)
    {
        var resp = await client.RawQuery(command.Sql, command.Parameters, ct).ConfigureAwait(false);
        if (!resp.HasErrors) return;
        var detail = resp.Errors
            .Select(e => e.GetType().GetProperty("Details")?.GetValue(e)?.ToString() ?? e.ToString())
            .FirstOrDefault() ?? "Unknown error";
        if (detail.Contains("Transaction conflict", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        throw new InvalidOperationException(detail);
    }

    /// <summary>
    /// Inserts a row with a client-generated id and returns the new id.
    /// Bypasses the typed <c>client.Create&lt;T&gt;</c> path entirely — that
    /// path goes through Dahomey.Cbor 1.26.1's deserializer which mis-decodes
    /// the SurrealDB v3 response shape for any row with PascalCase fields +
    /// nested objects and throws <c>CborException: Expected major type Map / Array</c>.
    ///
    /// Callers should NOT read the row back; build the DTO/entity from the
    /// input data + the returned id. Server-set columns like <c>created_at</c>
    /// via <c>DEFAULT time::now()</c> can be approximated with
    /// <c>DateTimeOffset.UtcNow</c> if needed.
    /// </summary>
    /// <param name="client">The SurrealDB client.</param>
    /// <param name="table">SurrealDB table name (e.g., <c>"company"</c>).</param>
    /// <param name="fields">Field name → value map. Names must be SurrealDB-canonical (snake_case).</param>
    /// <param name="idGenerator">Function that produces the new record id portion (without the <c>table:</c> prefix). Bring your own ULID/UUID/snowflake generator.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The id portion of the new record id (without the <c>table:</c> prefix).</returns>
    public static async Task<string> InsertWithIdAsync(
        this ISurrealDbClient client,
        string table,
        IDictionary<string, object?> fields,
        Func<string> idGenerator,
        CancellationToken ct = default)
    {
        if (idGenerator is null) throw new ArgumentNullException(nameof(idGenerator));

        // SurrealDB v3 distinguishes between NONE (field absent) and NULL
        // (field present with null value). For `option<…>` columns, sending
        // an explicit NULL fails with `Expected none | <type> but found
        // NULL`. Drop entries whose value is null so the column defaults to
        // NONE — callers don't need to pre-filter their dictionaries.
        var nonNullFields = fields.Where(kv => kv.Value is not null).ToList();
        var newId = idGenerator();
        var record = RecordId.From(table, newId);
        var setClauses = string.Join(", ", nonNullFields.Select(kv => $"{kv.Key} = ${kv.Key}"));
        var sql = $"CREATE $__rid SET {setClauses} RETURN NONE";
        var parameters = new Dictionary<string, object?>(nonNullFields.Count + 1)
        {
            ["__rid"] = record,
        };
        foreach (var kv in nonNullFields)
        {
            parameters[kv.Key] = kv.Value;
        }
        var resp = await client.RawQuery(sql, parameters, ct).ConfigureAwait(false);
        // RawQuery returns ok-with-errors on a per-statement basis. We only
        // submit one statement, so any error means the insert never happened —
        // surface it as an exception so callers don't accept a phantom success.
        if (resp.HasErrors)
        {
            // Preserve the original SurrealDB error text so callers can still
            // pattern-match on substrings like "Database index" / "already
            // contains" / "UNIQUE" to map index violations to a domain 409.
            var detail = resp.Errors
                .Select(e => e.GetType().GetProperty("Details")?.GetValue(e)?.ToString() ?? e.ToString())
                .FirstOrDefault() ?? "Unknown error";
            throw new InvalidOperationException($"CREATE on {table} failed: {detail}");
        }
        return newId;
    }
}
