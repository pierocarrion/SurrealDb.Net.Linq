# SurrealDb.Net.Linq

A fluent, parameter-safe query builder for [SurrealDB](https://surrealdb.com) that plugs into the official [SurrealDb.Net](https://www.nuget.org/packages/SurrealDb.Net) SDK.

Stop concatenating SurrealQL strings by hand. Stop sprinkling `RawQuery` calls across your repositories. Build typed `SELECT` / `LIVE SELECT` / `CREATE` / `UPDATE` / `UPSERT` / `DELETE` / `KILL` statements with an EF-Core-shaped surface, and execute them with one extension method on `ISurrealDbClient`.

```csharp
using SurrealDb.Net.Linq;

var cmd = SurrealQuery.From("user")
    .Where("email", SurrealOperator.Equals, "alice@example.com")
    .And("active", SurrealOperator.Equals, true)
    .OrderBy("created_at", SortDirection.Desc)
    .Limit(10)
    .Build();

var users = await client.ExecuteListAsync<UserRow>(cmd);
```

Every literal becomes a named parameter (`$p0`, `$p1`, …) — no SQL injection, no quoting bugs, no `RecordId.ToString()` round-trip surprises.

## Installation

```bash
dotnet add package SurrealDb.Net.Linq
dotnet add package SurrealDb.Net
```

Both packages are required. `SurrealDb.Net.Linq` declares `SurrealDb.Net` as a `PrivateAssets="all"` dependency on purpose: it does **not** ship `SurrealDb.Net` transitively, so downstream projects can pin their own version (including a vendored / patched copy) without a dual-DLL conflict. If you just want the regular SDK, install both as shown above.

Target frameworks: `net8.0`, `net9.0`, `net10.0`.

## Usage

### SELECT

```csharp
SurrealQuery.From("employee")
    .Select("id", "first_name", "department")
    .Where("department", SurrealOperator.Equals, "engineering")
    .And("salary", SurrealOperator.Greater, 50_000)
    .OrderBy("first_name")
    .Limit(50)
    .Fetch("manager")
    .Build();
```

### LIVE SELECT

```csharp
SurrealQuery.Live("ticket")
    .Where("assignee", SurrealOperator.Equals, currentUserId)
    .Build();
```

### CREATE

```csharp
SurrealQuery.Create("user")
    .Set("email", "bob@example.com")
    .Set("active", true)
    .SetExpr("created_at", "time::now()")
    .ReturnAfter()
    .Build();
```

### UPDATE by record id (CBOR-safe)

```csharp
SurrealQuery.UpdateRecord(RecordId.From("user", id))
    .Set("last_login_at", DateTimeOffset.UtcNow)
    .ReturnNone()
    .Build();
```

`UpdateRecord` / `UpsertRecord` bind the record id as a CBOR parameter so you avoid the `user:01HZ…` vs `user:⟨01HZ…⟩` string-rendering mismatch that silently matches no rows.

### DELETE

```csharp
SurrealQuery.Delete("session")
    .Where("expires_at", SurrealOperator.Less, DateTimeOffset.UtcNow)
    .Build();
```

### KILL

```csharp
SurrealQuery.Kill(liveQueryId);
```

### Raw escape hatch

For exotic SurrealQL the builders don't cover (graph traversals, transactions, `math::*` functions):

```csharp
SurrealQuery.Raw(
    "SELECT ->wrote->article.* AS articles FROM $author FETCH articles",
    new Dictionary<string, object?> { ["author"] = authorId });
```

## Execution

Six extension methods on `ISurrealDbClient`:

| Method | Returns | Use for |
| --- | --- | --- |
| `ExecuteAsync(cmd)` | `SurrealDbResponse` | full envelope |
| `ExecuteScalarAsync<T>(cmd)` | `T?` | first statement → first value |
| `ExecuteListAsync<T>(cmd)` | `List<T>` | first statement → row list |
| `ExecuteNoResultAsync(cmd)` | `Task` | UPDATE/DELETE/KILL, surfaces ASSERT/UNIQUE errors |
| `InsertWithIdAsync(table, fields, idGenerator)` | `string` (new id) | row creation that survives Dahomey.Cbor 1.26.1 PascalCase-with-nested-objects deserialization |

### Why `InsertWithIdAsync`?

The typed `client.Create<T>(...)` path goes through Dahomey.Cbor 1.26.1, which mis-decodes the SurrealDB v3 response shape for rows that mix PascalCase fields with nested objects. `InsertWithIdAsync` works around that by sending `CREATE $rid SET ... RETURN NONE` with a client-generated id and never deserializing the response row. Pass any id generator you want:

```csharp
var newId = await client.InsertWithIdAsync(
    table: "company",
    fields: new Dictionary<string, object?>
    {
        ["name"] = "Acme",
        ["tax_id"] = "20123456789",
    },
    idGenerator: () => Ulid.NewUlid().ToString());
```

## Recipe: two-step typed SELECT with PascalCase rows

The same Dahomey.Cbor 1.26.1 bug bites typed `RawQuery` reads. The robust pattern: filter on the server, return only ids as strings, then re-fetch each row with `client.Select<T>`.

```csharp
var ids = await client.ExecuteListAsync<string?>(
    SurrealQuery.From("employee")
        .SelectValue("<string>id")
        .Where("department", SurrealOperator.Equals, "engineering")
        .OrderBy("hire_date", SortDirection.Desc)
        .Limit(50)
        .Build());

var rows = new List<EmployeeRow>();
foreach (var id in ids.OfType<string>())
{
    var row = await client.Select<EmployeeRow>(RecordId.Parse(id));
    if (row is not null) rows.Add(row);
}
```

## License

MIT — see [LICENSE](LICENSE).

## Status

`0.2.x` — early release. The builder surface is the EF-Core-shaped subset that's been battle-tested in production (multi-tenant SaaS on SurrealDB v3). It is not a full LINQ-to-SurrealQL provider — there is no `Expression<Func<T, bool>>` translation, by design. If you want LINQ expression trees, this is not that library.

### Changelog

- **0.2.0** — `SurrealDb.Net` is now a `PrivateAssets="all"` dependency. Consumers must install `SurrealDb.Net` themselves. This lets projects with a vendored or patched `SurrealDb.Net` avoid dual-DLL conflicts.
- **0.1.0** — initial release.
