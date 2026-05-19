# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.2.1...v0.3.0
[0.2.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/releases/tag/v0.1.0
