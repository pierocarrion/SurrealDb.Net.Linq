# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.4] - 2026-05-19

### Added
- `CborMapToDictionaryConverter` now implements a fully working `Write` path:
  it dispatches each value by runtime type (primitives written directly;
  nested dictionaries / lists recurse; complex types — `DateTime`,
  `DateTimeOffset`, `Guid`, `decimal`, SurrealDB `RecordId`, etc. — delegate
  to the host `CborOptions` registry via the non-generic
  `ICborConverter.Write(ref CborWriter, object)` surface).
- `CborOptions.UseSurrealSnakeCase()` now registers
  `CborMapToDictionaryConverter` globally as the converter for
  `Dictionary<string, object?>`. This fixes a second class of `[XX] Expected
  major type Map (5)` failures: Dahomey.Cbor 1.26.1's stock
  `DictionaryConverter<string, object>` resolves each value via
  `ObjectConverter<object>` (which expects a Map), so any SurrealDB row with
  an `object FLEXIBLE` column (e.g. `country_catalog.working_hours`,
  `customer.document`) explodes the moment it carries a primitive value.
  The new write path keeps `RawQuery`'s parameter map serialization
  behaviour identical to Dahomey's default.

### Removed
- The selective-opt-in guidance for `CborMapToDictionaryConverter` —
  it's now safe (and recommended) to register globally via
  `UseSurrealSnakeCase()`.

## [0.3.3] - 2026-05-19

### Added
- `DateTimeOffsetConverter` — handles SurrealDB's `tag(12) [seconds, nanos]`
  datetime wire shape for `DateTimeOffset`. The official `SurrealDb.Net 0.10.x`
  package only registers a converter for `DateTime`; `DateTimeOffset` falls
  back to Dahomey's generic `ObjectConverter<DateTimeOffset>`, which expects
  a CBOR Map and throws `[XX] Expected major type Map (5)` the instant any
  row with a non-null `DateTimeOffset` column (e.g. `created_at`,
  `updated_at`) is round-tripped through `Select<T>(table)` or
  `Select<T>(RecordId)`.
- `CborOptions.UseSurrealSnakeCase()` now also registers
  `DateTimeOffsetConverter` so the fix is automatic for every consumer.

### Fixed
- `Select<T>(table)` / `Select<T>(RecordId)` over rows with `DateTimeOffset`
  members no longer fails with `CborException: Expected major type Map (5)`.

## [0.3.2] - 2026-05-19

### Changed
- `CborOptions.UseSurrealSnakeCase()` no longer registers
  `CborMapToDictionaryConverter` as the global converter for
  `Dictionary<string, object?>`. Registering it globally hijacked the WRITE
  path that `SurrealDb.Net` uses to serialize `RawQuery` parameter maps,
  raising `NotSupportedException` on every parameterized query. The converter
  remains available for selective opt-in via
  `[CborConverter(typeof(CborMapToDictionaryConverter))]` on row fields that
  carry free-form `object FLEXIBLE` sub-objects.
- `CborMapToDictionaryConverter` is documented as read-only and now throws a
  clearer error from `Write` to surface accidental global registration.

## [0.3.1] - 2026-05-19

### Added
- `SurrealDb.Net.Linq.Cbor` namespace with the CBOR plumbing that restores
  SurrealDB-friendly defaults the official `SurrealDb.Net 0.10.x` package
  dropped:
  - `SnakeCaseCborNamingConvention` — `INamingConvention` that maps CLR member
    names to snake_case (`HireDate` → `hire_date`, `User_Id` → `user_id`,
    already-snake_case names preserved). Honors `[Column("name")]` overrides.
  - `CborMapToDictionaryConverter` — converter for
    `Dictionary<string, object?>?` so rows that mix typed fields with
    free-form `object FLEXIBLE` sub-objects (e.g. `country_catalog`) decode
    without throwing `CborException: Expected major type Map (5)`.
  - `CborOptions.UseSurrealSnakeCase()` — single-call wire-up of the two above.
    Plug into `new SurrealDbClient(opts, configureCborOptions: o => o.UseSurrealSnakeCase())`.
- `Dahomey.Cbor` is now a regular (public) `PackageReference` since the new
  API surface exposes types from it; pinned to `1.26.1` to match SurrealDb.Net.

## [0.3.0] - 2026-05-19

### Added
- Typed builder entry points: `SurrealQuery.From<T>(table)`,
  `SurrealQuery.Live<T>(table)`, and `SurrealQuery.Delete<T>(table)` return
  generic builders that accept `Expression<Func<T, bool>>` predicates in
  `Where` / `And` / `Or`.
- `ExpressionToWhere` visitor: translates comparison + logical operators,
  member access (including nested chains), boolean member shorthand, null
  comparisons (`IS NONE` / `IS NOT NONE`), closure capture, `string.Contains` /
  `StartsWith` / `EndsWith`, `string.IsNullOrEmpty`, and collection `Contains`
  (`IN` and `CONTAINS`). Unsupported shapes throw `NotSupportedException` with
  a pointer to `WhereRaw`.
- `MemberNameResolver`: maps CLR members to SurrealQL field names —
  `[JsonPropertyName("…")]` wins, otherwise snake_case fallback.
- xUnit test project under `tests/SurrealDb.Net.Linq.Tests/` covering the
  public builder surface, every supported visitor shape, and the failure mode
  for unsupported expressions. `IsPackable=false` so tests never ship in the
  NuGet package.

### Changed
- Repository restructure: source files split per type into `Abstractions/`,
  `Builders/`, `Extensions/`, and `Internal/` folders.
- Shared MSBuild properties moved to repo-level `Directory.Build.props`.
- Package versions centralized via `Directory.Packages.props` (Central Package
  Management).
- Added repo-level `.editorconfig` codifying the existing C# style.

## [0.2.0]

### Changed
- `SurrealDb.Net` is now declared `PrivateAssets="all"`. Consumers must install
  `SurrealDb.Net` themselves. This lets projects with a vendored or patched
  `SurrealDb.Net` avoid dual-DLL conflicts.

## [0.1.0]

### Added
- Initial release. Fluent builders for `SELECT` / `LIVE SELECT` / `CREATE` /
  `UPDATE` / `UPSERT` / `DELETE` / `KILL`, plus `Raw` escape hatch.
- `ISurrealDbClient` execution extensions: `ExecuteAsync`,
  `ExecuteScalarAsync<T>`, `ExecuteListAsync<T>`, `ExecuteNoResultAsync`,
  `InsertWithIdAsync`.

[Unreleased]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.3.4...HEAD
[0.3.4]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.3.3...v0.3.4
[0.3.3]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.3.2...v0.3.3
[0.3.2]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.3.1...v0.3.2
[0.3.1]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.2.1...v0.3.0
[0.2.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/releases/tag/v0.1.0
