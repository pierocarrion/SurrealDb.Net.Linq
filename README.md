# SurrealDb.Net.Linq

[![NuGet](https://img.shields.io/nuget/v/SurrealDb.Net.Linq.svg)](https://www.nuget.org/packages/SurrealDb.Net.Linq)

A fluent, parameter-safe query builder for [SurrealDB](https://surrealdb.com) that plugs into the official [SurrealDb.Net](https://www.nuget.org/packages/SurrealDb.Net) SDK.

NuGet package: **<https://www.nuget.org/packages/SurrealDb.Net.Linq>**

Stop concatenating SurrealQL strings by hand. Stop sprinkling `RawQuery` calls across your repositories. Build typed `SELECT` / `LIVE SELECT` / `CREATE` / `UPDATE` / `UPSERT` / `DELETE` / `KILL` statements with an EF-Core-shaped surface — including typed `Expression<Func<T, bool>>` `Where` lambdas — and execute them with one extension method on `ISurrealDbClient`.

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

Target frameworks: `net8.0`, `net9.0`, `net10.0`, `netstandard2.1` (covers .NET Core 3.0+, .NET 5/6/7, Mono, Xamarin, Unity).

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

### SELECT with typed `Where` lambdas

`SurrealQuery.From<T>(table)` returns a generic builder whose `Where` /
`And` / `Or` accept `Expression<Func<T, bool>>` predicates. Field names are
derived from `[JsonPropertyName("…")]` if present, otherwise from the property
name in snake_case (`HireDate` → `hire_date`).

```csharp
var department = "engineering";
var minSalary  = 50_000;

SurrealQuery.From<Employee>("employee")
    .Select("id", "first_name", "department")
    .Where(e => e.Department == department && e.Salary >= minSalary && e.Active)
    .OrderBy("hire_date", SortDirection.Desc)
    .Limit(50)
    .Build();
```

Supported in the visitor (MVP scope): `==` `!=` `<` `<=` `>` `>=` `&&` `||` `!`,
member access (including nested chains like `u.Address.City`), boolean member
shorthand (`u => u.Active`), null comparisons (`u.Email == null` →
`email IS NONE`), captured locals/method calls (parameterised), `string.Contains`
/ `StartsWith` / `EndsWith`, `string.IsNullOrEmpty`, and collection `Contains`
(local collection → `field IN $p`, member collection → `field CONTAINS $p`).

Anything outside that surface (graph traversals, subqueries, `math::*` calls,
projections) raises `NotSupportedException` — drop down to `WhereRaw(...)` or
`SurrealQuery.Raw(...)`.

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

### Transactions

Group multiple statements into a single atomic `BEGIN; … COMMIT;` command. Each statement keeps its own parameters; the builder automatically rewrites parameter names so collisions across statements cannot happen.

```csharp
var tx = SurrealQuery.BeginTransaction()
    .Add(SurrealQuery.Create("audit").Set("action", "user_created").Build())
    .Add(SurrealQuery.UpdateRecord(RecordId.From("user", id)).Set("active", true).Build())
    .Commit()
    .Build();

await client.ExecuteNoResultAsync(tx);
```

Use `.Rollback()` instead of `.Commit()` to emit `BEGIN; … CANCEL;`.

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
| `ExecuteCountAsync(cmd)` | `long` | first statement → first value cast to `long` |
| `ExecuteAnyAsync<T>(cmd)` | `bool` | whether the first statement returned at least one row |
| `ExecuteNoResultAsync(cmd)` | `Task` | UPDATE/DELETE/KILL/transactions, surfaces ASSERT/UNIQUE errors |
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

## Contributing

Bug reports and pull requests are welcome on
[GitHub](https://github.com/pierocarrion/SurrealDb.Net.Linq).

### Reporting a bug

Open an [issue](https://github.com/pierocarrion/SurrealDb.Net.Linq/issues/new)
and include:

1. **What you tried** — the smallest builder chain that reproduces the problem,
   ideally as a copy-pasteable snippet.
2. **What you expected** — the SurrealQL you wanted the builder to emit, or the
   client behaviour you expected.
3. **What you got** — the actual generated SurrealQL (call `.Build().Sql` and
   inspect `.Parameters`) and/or the exception message + stack trace.
4. **Environment** — SurrealDB server version, `SurrealDb.Net` version,
   `SurrealDb.Net.Linq` version, target framework.

For lambda translation issues, paste the predicate and the
`NotSupportedException` message — the visitor explicitly tells you which node
it couldn't translate.

### Submitting a pull request

1. Fork the repo and create a topic branch from `master`
   (`git checkout -b fix/short-description`).
2. Build and test locally — both must be green:
   ```bash
   dotnet build  -c Release
   dotnet test
   ```
3. Add or update tests under `tests/SurrealDb.Net.Linq.Tests/` for any behaviour
   change. The existing suite (118 tests) covers the public builder surface and
   every shape the expression visitor accepts — follow the same patterns.
4. Keep the public API minimal. New surface lands in `src/SurrealDb.Net.Linq/`;
   internals (anything reused only by builders) goes under `Internal/`.
5. Update `CHANGELOG.md` under the `[Unreleased]` section.
6. Open a PR with a short description of the problem and the chosen fix.

The package itself ships only the library DLL + XML docs (~65 KB nupkg) — test
projects live under `tests/` with `IsPackable=false` so they never end up in
the published artifact.

## License

MIT — see [LICENSE](LICENSE).

## Status

`0.8.x` — early release. The builder surface is the EF-Core-shaped subset
that's been battle-tested in production (multi-tenant SaaS on SurrealDB v3),
plus a growing set of SurrealQL clauses (INSERT, RELATE, set operations,
CONTENT/MERGE/PATCH, SPLIT, EXPLAIN, PARALLEL, TIMEOUT, VERSION).

`Expression<Func<T, bool>>` translation covers comparisons, logical ops,
null checks, member chains, `string.Contains/StartsWith/EndsWith/ToLower/
ToUpper/Trim/Replace/IsNullOrEmpty/IsNullOrWhiteSpace`, collection
`Contains` (IN/CONTAINS), `DateTime` properties (`time::*` functions), and
`Nullable<T>` null checks. Arithmetic, subqueries and graph traversal are
not supported — drop down to `WhereRaw(...)` for those.

### New in 0.8.0

- **Granular CBOR opt-ins**: `UseSnakeCaseNaming`, `UseDateTimeOffsetConverter`,
  `UseMapToDictionaryConverter` — pick only what you need.
- **CodeQL + Dependabot** enabled.

### New in 0.7.0

- **`SurrealQuery.Insert`** (DML INSERT — `Columns`/`Values` multi-row +
  `OnDuplicateKeyUpdate`).
- **`SurrealQuery.Relate`** for graph edges (`RELATE source -> edge -> target`,
  `RELATE UNIQUE` variant).
- **Set operations** (`UNION` / `UNION ALL` / `INTERSECT` / `DIFFERENCE`).
- **8 new operators**: `Outside`, `Intersects`, `AllInside`, `AnyInside`,
  `AllOutside`, `AnyOutside`, `Matches` (regex `~`), `NotMatches` (`!~`).
- **CONTENT/MERGE/PATCH** body modes on Create/Update.
- **SELECT clauses**: `SPLIT ON`, `EXPLAIN [FULL]`, `PARALLEL`, `TIMEOUT`,
  `VERSION` (time-travel).
- **Visitor**: `DateTime` properties → `time::*` functions, more string
  methods supported.

### New in 0.6.0

- **Generic typed builders** `Create<T>`, `Update<T>`, `Upsert<T>` with
  lambda-based `Set(Expression<Func<T,K>>, value)` selectors.
- **`SurrealReturn` enum** replacing magic string literals.
- **Delete parity**: `Only()`, `Limit()`, `Start()`, `ReturnAfter`,
  `ReturnFields`.
- **Execution helpers**: `ExecuteSingleAsync<T>`,
  `ExecuteListStrictAsync<T>`, `ExecutePagedAsync<T>` (with `HasNext` flag),
  `ExecuteTransactionAsync(builder, retryOnConflict, maxRetries)`.

### New in 0.5.0

- **`ExecuteScalarStrictAsync<T>` / `ExecuteCountStrictAsync`** — variants
  that propagate errors instead of swallowing them. Legacy versions kept
  unchanged for back-compat.
- **CI**: matrix builds on ubuntu/windows/macos, mandatory `dotnet test`
  before every PR merge and before publishing to NuGet.
- **Bug fixes**: `Transaction conflict` detection (silently broken since
  0.3.x — the wrong error property was being read); `$word` in string
  literals no longer corrupted by the transaction builder regex;
  `ParameterBag.Snapshot()` now returns a defensive copy.
- **Validation**: all builders reject null/empty/negative arguments.

### CBOR configuration

The official `SurrealDb.Net 0.10.x` package dropped several CBOR defaults that
SurrealDB v3 relies on. `SurrealDb.Net.Linq` restores them so rows decode
correctly:

```csharp
using SurrealDb.Net.Linq.Cbor;

var client = new SurrealDbClient(endpoint,
    configureCborOptions: o => o.UseSurrealSnakeCase());
```

`UseSurrealSnakeCase()` registers:

- `SnakeCaseCborNamingConvention` — maps CLR member names to snake_case
  (`HireDate` → `hire_date`). Honors `[Column("name")]` overrides.
- `CborMapToDictionaryConverter` — fixes `Expected major type Map (5)` errors
  on rows that contain free-form `object FLEXIBLE` columns (e.g.
  `country_catalog.working_hours`, `customer.document`).
- `DateTimeOffsetConverter` — fixes the same error for `DateTimeOffset` columns
  (`created_at`, `updated_at`, etc.) because the official package only registers
  a converter for `DateTime`.

Without this, any row containing a `DateTimeOffset` or an `object` field with a
primitive value explodes during `Select<T>`. The call is optional but strongly
recommended.

### Changelog

See [CHANGELOG.md](CHANGELOG.md).
