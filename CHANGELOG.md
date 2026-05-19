# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/releases/tag/v0.1.0
