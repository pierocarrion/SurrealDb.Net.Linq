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

    /// <summary>
    /// Begin a typed <c>SELECT … FROM &lt;target&gt;</c> statement. Returns a
    /// builder that accepts <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c>
    /// predicates in <c>Where</c>/<c>And</c>/<c>Or</c>.
    /// </summary>
    public static SurrealSelectBuilder<T> From<T>(string target) => new(target, isLive: false);

    /// <summary>Begin a <c>LIVE SELECT … FROM &lt;target&gt;</c> statement.</summary>
    public static SurrealSelectBuilder Live(string target) => new(target, isLive: true);

    /// <summary>Begin a typed <c>LIVE SELECT … FROM &lt;target&gt;</c> statement.</summary>
    public static SurrealSelectBuilder<T> Live<T>(string target) => new(target, isLive: true);

    /// <summary>Begin a <c>CREATE &lt;target&gt;</c> statement.</summary>
    public static SurrealCreateBuilder Create(string target) => new(target);

    /// <summary>
    /// Begin a typed <c>CREATE &lt;target&gt;</c> statement with lambda-based
    /// <see cref="SurrealCreateBuilder{T}.Set{K}"/> selectors. Field names are
    /// resolved from <c>[JsonPropertyName]</c> or snake_case fallback.
    /// </summary>
    public static SurrealCreateBuilder<T> Create<T>(string target) => new(target);

    /// <summary>Begin an <c>UPDATE &lt;target&gt;</c> statement against a literal target string.</summary>
    public static SurrealUpdateBuilder Update(string target) => new(target, upsert: false);

    /// <summary>Typed <c>UPDATE</c> with lambda Where/Set selectors.</summary>
    public static SurrealUpdateBuilder<T> Update<T>(string target) => new(target, upsert: false);

    /// <summary>
    /// Begin an <c>UPDATE</c> against a record id passed as a CBOR parameter — the
    /// preferred form for typed callers. Avoids the <c>RecordId.ToString()</c>
    /// round-trip mismatch where a record id like <c>user:01HZ…</c> can be
    /// rendered with or without angle brackets and silently match no rows.
    /// </summary>
    public static SurrealUpdateBuilder UpdateRecord(object recordId)
    {
        Arg.NotNull(recordId);
        return new SurrealUpdateBuilder(recordId, upsert: false);
    }

    /// <summary>Typed <c>UPDATE</c> against a record id with lambda selectors.</summary>
    public static SurrealUpdateBuilder<T> UpdateRecord<T>(object recordId)
    {
        Arg.NotNull(recordId);
        return new SurrealUpdateBuilder<T>(recordId, upsert: false);
    }

    /// <summary>Begin an <c>UPSERT &lt;target&gt;</c> statement against a literal target string.</summary>
    public static SurrealUpdateBuilder Upsert(string target) => new(target, upsert: true);

    /// <summary>Typed <c>UPSERT</c> with lambda Where/Set selectors.</summary>
    public static SurrealUpdateBuilder<T> Upsert<T>(string target) => new(target, upsert: true);

    /// <summary><c>UPSERT</c> against a record id passed as a CBOR parameter.</summary>
    public static SurrealUpdateBuilder UpsertRecord(object recordId)
    {
        Arg.NotNull(recordId);
        return new SurrealUpdateBuilder(recordId, upsert: true);
    }

    /// <summary>Typed <c>UPSERT</c> against a record id with lambda selectors.</summary>
    public static SurrealUpdateBuilder<T> UpsertRecord<T>(object recordId)
    {
        Arg.NotNull(recordId);
        return new SurrealUpdateBuilder<T>(recordId, upsert: true);
    }

    /// <summary>Begin a <c>DELETE &lt;target&gt;</c> statement.</summary>
    public static SurrealDeleteBuilder Delete(string target) => new(target);

    /// <summary>Begin a typed <c>DELETE &lt;target&gt;</c> statement with Expression-based predicates.</summary>
    public static SurrealDeleteBuilder<T> Delete<T>(string target) => new(target);

    /// <summary>Begin an <c>INSERT INTO</c> statement (DML INSERT — the missing piece).</summary>
    public static SurrealInsertBuilder Insert(string target) => new(target);

    /// <summary>
    /// Begin a <c>RELATE source -&gt; edge -&gt; target</c> statement. Creates
    /// a graph edge between two records. Source and target are parameterized
    /// (record ids serializados via CBOR).
    /// </summary>
    public static SurrealRelateBuilder Relate(string source, string edge, string target) =>
        new(source, edge, target);

    /// <summary>Begin a <c>RELATE UNIQUE …</c> — only creates the edge if it doesn't already exist.</summary>
    public static SurrealRelateBuilder RelateUnique(string source, string edge, string target) =>
        new(source, edge, target, unique: true);

    /// <summary>Begin a set operation (UNION/UNION ALL/INTERSECT/DIFFERENCE) over N SELECT commands.</summary>
    public static SurrealSetOperationBuilder SetOperation() => new();

    /// <summary>Build a <c>KILL $live</c> statement.</summary>
    public static ISurrealCommand Kill(Guid liveId) =>
        new SurrealCommand("KILL $live", new Dictionary<string, object?> { ["live"] = liveId });

    /// <summary>Wrap a hand-written SurrealQL string. Use sparingly — prefer the typed builders.</summary>
    public static ISurrealCommand Raw(string sql, IDictionary<string, object?>? parameters = null)
    {
        Arg.NotNullOrWhiteSpace(sql);
        return new SurrealCommand(sql, parameters is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(parameters));
    }

    /// <summary>Begin a multi-statement transaction builder.</summary>
    public static SurrealTransactionBuilder BeginTransaction() => new();
}
