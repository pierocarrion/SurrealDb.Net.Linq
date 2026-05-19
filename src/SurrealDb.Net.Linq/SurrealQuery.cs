namespace SurrealDb.Net.Linq;

/// <summary>
/// Fluent builder for SurrealQL statements. Repositories never concatenate
/// SurrealQL strings by hand: every value gets a named parameter
/// (<c>$p0</c>, <c>$p1</c>…), reserved words and operators are emitted by the
/// builder, and the builder's <c>Build()</c> method returns the
/// (sql, parameters) pair that <c>ISurrealDbClient.RawQuery</c> expects.
///
/// The surface is the EF-Core-shaped subset most apps actually use:
/// SELECT / LIVE SELECT, CREATE, UPDATE, UPSERT, DELETE, KILL, plus a
/// <see cref="Raw"/> escape hatch for anything exotic (graph traversals,
/// <c>math::*</c>, transactions, etc.).
/// </summary>
public static class SurrealQuery
{
    /// <summary>Begin a <c>SELECT … FROM &lt;target&gt;</c> statement.</summary>
    public static SurrealSelectBuilder From(string target) => new(target, isLive: false);

    /// <summary>Begin a <c>LIVE SELECT … FROM &lt;target&gt;</c> statement.</summary>
    public static SurrealSelectBuilder Live(string target) => new(target, isLive: true);

    /// <summary>Begin a <c>CREATE &lt;target&gt;</c> statement.</summary>
    public static SurrealCreateBuilder Create(string target) => new(target);

    /// <summary>Begin an <c>UPDATE &lt;target&gt;</c> statement against a literal target string.</summary>
    public static SurrealUpdateBuilder Update(string target) => new(target, upsert: false);

    /// <summary>
    /// Begin an <c>UPDATE</c> against a record id passed as a CBOR parameter — the
    /// preferred form for typed callers. Avoids the <c>RecordId.ToString()</c>
    /// round-trip mismatch where a record id like <c>user:01HZ…</c> can be
    /// rendered with or without angle brackets and silently match no rows.
    /// </summary>
    public static SurrealUpdateBuilder UpdateRecord(object recordId) => new(recordId, upsert: false);

    /// <summary>Begin an <c>UPSERT &lt;target&gt;</c> statement against a literal target string.</summary>
    public static SurrealUpdateBuilder Upsert(string target) => new(target, upsert: true);

    /// <summary><c>UPSERT</c> against a record id passed as a CBOR parameter.</summary>
    public static SurrealUpdateBuilder UpsertRecord(object recordId) => new(recordId, upsert: true);

    /// <summary>Begin a <c>DELETE &lt;target&gt;</c> statement.</summary>
    public static SurrealDeleteBuilder Delete(string target) => new(target);

    /// <summary>Build a <c>KILL $live</c> statement.</summary>
    public static ISurrealCommand Kill(Guid liveId) =>
        new SurrealCommand("KILL $live", new Dictionary<string, object?> { ["live"] = liveId });

    /// <summary>Wrap a hand-written SurrealQL string. Use sparingly — prefer the typed builders.</summary>
    public static ISurrealCommand Raw(string sql, IDictionary<string, object?>? parameters = null) =>
        new SurrealCommand(sql, parameters is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(parameters));
}
