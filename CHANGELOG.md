# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.7.0] - 2026-06-21

### Added
- `CborOptions.UseSnakeCaseNaming()` / `UseDateTimeOffsetConverter()` /
  `UseMapToDictionaryConverter()` — opt-ins granulares. Permite setups donde
  sólo uno de los converters es necesario, sin arrastrar los otros dos.
- `codeql.yml` workflow: análisis semanal de seguridad C# con queries
  `security-and-quality`.
- `dependabot.yml`: PRs automáticos para bumps de NuGet y GitHub Actions.
- `SurrealCborOptionsExtensionsTests` (6 tests): cubren los 3 opt-ins
  granulares y el composite.

### Changed
- `UseSurrealSnakeCase()` ahora es un composite que llama a los 3 opt-ins.
  El comportamiento para callers existentes es idéntico.

### Notes
- TODO(0.8.0): detectar tagged values en
  `CborMapToDictionaryConverter.ReadCborValueIntoObject` para que
  `DateTimeOffset` / `Guid` dentro de columnas `object FLEXIBLE` se
  materialicen correctamente (hoy se ven como array suelto o null).

## [0.6.0] - 2026-06-21

### Added
- `SurrealInsertBuilder` (DML INSERT) — `Columns(params string[])`,
  `Values(params object?[])` multi-row, `OnDuplicateKeyUpdate`, todos los
  RETURN modes. Era el único DML que faltaba en la lib.
- `SurrealRelateBuilder` — `RELATE source -> edge -> target` para crear
  aristas de grafo. Variante `RelateUnique` para `RELATE UNIQUE`.
- `SurrealSetOperationBuilder` — `UNION` / `UNION ALL` / `INTERSECT` /
  `DIFFERENCE` composicional sobre N `ISurrealCommand` con rebase de
  parámetros prefix-everything (mismo esquema que transaction).
- Select clauses: `SPLIT ON`, `EXPLAIN [FULL]`, `PARALLEL`, `TIMEOUT`,
  `VERSION` (time-travel queries con change feed).
- Create/Update body modes: `CONTENT $expr`, `MERGE $expr`, `PATCH $expr`
  (RFC 6902 JSON Patch). Mutuamente excluyentes con SET, validado en Build.
- 8 nuevos operadores: `Outside`, `Intersects`, `AllInside`, `AnyInside`,
  `AllOutside`, `AnyOutside`, `Matches` (regex `~`), `NotMatches` (`!~`).
- Visitor: `DateTime`/`DateTimeOffset` properties ahora renderizan como
  funciones `time::*` (`r.CreatedAt.Year` → `time::year(created_at)`).
  Antes se emitía como `created_at.year` (campo inexistente → SQL válido
  pero semánticamente erróneo).
- Visitor: `string.ToLower` / `ToUpper` / `Trim` / `Replace` /
  `IsNullOrWhiteSpace` ahora se traducen a `string::*` functions.
- Visitor: `string.Length` → `string::len(field)`.
- 14 tests nuevos (NewBuildersTests, VisitorExtensionsTests).

### Changed
- `SurrealOperator.Like` (que emite `string::contains(...)`) marcado en el
  XML doc como legacy; los nuevos `Matches`/`NotMatches` son los operadores
  regex nativos de SurrealDB recomendados.

### Fixed
- Visitor: side-effect bug en single-arg method dispatch — el placeholder
  se "quemaba" cuando el método no matcheaba,corrigiendo el numbering en
  casos como `Email.Replace("@", "_") == "x"`.
- Visitor: `Nullable<T>.HasValue` / `.Value` ahora rechazados con mensaje
  accionable antes de que el bool-member shorthand los renderice como
  campos inexistentes.

## [0.5.0] - 2026-06-21

### Added
- `SurrealCreateBuilder<T>` con `Set<K>(Expression<Func<T,K>>, object?)`,
  `SetExpr`, `SetIfPresent` lambdas. Resolución vía
  `[JsonPropertyName]` o snake_case fallback, soporta cadenas anidadas
  (`u.Address.City`).
- `SurrealUpdateBuilder<T>` con Set lambdas + `Where`/`And`/`Or`
  `Expression<Func<T,bool>>`.
- `SurrealQuery.Create<T>`, `Update<T>`, `Upsert<T>`, `UpdateRecord<T>`,
  `UpsertRecord<T>` entrypoints.
- `SurrealReturn` enum + `SurrealReturnRenderer` (None/Before/After/Diff)
  reemplazando literals "BEFORE"/"AFTER"/"NONE"/"DIFF" internos.
- `ReturnFields(params string[])` en Create/Update/Delete para
  `RETURN field1, field2, …` syntax.
- Paridad DELETE: `Only()`, `Limit(int)`, `Start(int)`, `ReturnAfter`,
  `Return(SurrealReturn)`, `ReturnFields`.
- `ExecuteSingleAsync<T>` — primera fila o default.
- `ExecuteListStrictAsync<T>` / `ExecuteAnyStrictAsync<T>` — variantes
  estrictas que propagan errores.
- `ExecutePagedAsync<T, page, pageSize>` — paginación con detección de
  HasNext en una sola query (LIMIT pageSize+1).
- `ExecuteTransactionAsync(builder, retryOnConflict, maxRetries)` con
  backoff exponencial + jitter para reintentar "Transaction conflict".
- 14 tests cubriendo generic builders, SurrealReturn enum, delete parity.

## [0.4.0] - 2026-06-21

### Added
- **CI**: build matrix (ubuntu/windows/macos), ejecución de `dotnet test` en
  cada push/PR, recolección de cobertura Coverlet (Cobertura format) y upload
  de artefactos. Antes de 0.4.0 el CI sólo hacía `dotnet build` + `dotnet pack`
  dry-run — un PR que rompía tests podía mergear verde.
- **CI (publish)**: gate `dotnet test` antes del push a NuGet. Un tag roto
  ya no llega al feed público.
- `ExecuteScalarStrictAsync<T>` y `ExecuteCountStrictAsync`: variantes estrictas
  que propagan errores de RPC/deserialización en lugar de tragarlos. Las
  versiones legacy `ExecuteScalarAsync`/`ExecuteCountAsync` se quedan sin
  cambios (no marcadas obsoletas) para no romper builds existentes con
  `TreatWarningsAsErrors`. Recomendado: migrar a los `*Strict` en nuevos
  código.
- `Internal/Arg`: helper de validación con polyfill compile-time de
  `[CallerArgumentExpression]` para `netstandard2.1`. Reemplaza el uso directo
  de `ArgumentNullException.ThrowIfNull` (que no existe en netstandard2.1).
- `Internal/SurrealDbErrorExtractor`: punto único de reflexión sobre errores
  SurrealDb.Net — antes repartido en 2 callers con reflexión duplicada.
- `ISurrealCommand.Placeholders` (lista ordenada, sin `$`): permite a la
  transaction reescribir parámetros sin parsear SQL. Default: vacío
  (compatibilidad con implementaciones externas).
- `ParameterBag.GetPlaceholders()`: orden de inserción para alimentar la
  property nueva.
- Validación de argumentos en TODOS los métodos públicos de los builders
  (`Select`, `Create`, `Update`, `Upsert`, `Delete`, `Raw`, `InsertWithIdAsync`):
  `ArgumentNullException` / `ArgumentException` / `ArgumentOutOfRangeException`
  para null, vacío, negativo.
- Backtick-escaping de nombres de campo en `InsertWithIdAsync` (neutraliza la
  única superficie de inyección SQL de la lib; los valores seguían
  parametrizados).
- NSubstitute 5.3.0 + coverlet.collector 6.0.2 en el test project (cobertura
  y mock framework para los tests nuevos de extensiones).
- 71 tests nuevos:
  - `SurrealClientExtensionsTests` (11): validación de `InsertWithIdAsync`,
    paths de `*Strict`, propagación de cancellation, fixture de error
    responses vía reflexión.
  - `SurrealDbErrorExtractorTests` (8): paths Message/Details/ToString/empty.
  - `BuilderValidationTests` (35): validaciones de todos los builders.
  - `ParameterBagTests` (4 nuevos): snapshot defensivo, GetPlaceholders.
  - `SurrealTransactionBuilderTests` (5 nuevos): regresión T7, Placeholders.
- Multi-target `netstandard2.1` además de `net8.0`, `net9.0` y `net10.0`. El
  paquete ahora corre en .NET Core 3.0/3.1, .NET 5, 6 y 7, Mono, Xamarin y
  Unity. `System.Text.Json` es ahora PackageReference explícito para el
  target `netstandard2.1` (relanzado desde la sección Unreleased previa).
- `SurrealQuery.BeginTransaction()` y `SurrealTransactionBuilder`: construyen
  transacciones multi-statement `BEGIN; … COMMIT;` / `BEGIN; … CANCEL;`
  desde `ISurrealCommand` existentes (relanzado desde Unreleased previa).
- `ExecuteCountAsync(this ISurrealDbClient, ISurrealCommand)` y
  `ExecuteAnyAsync<T>(this ISurrealDbClient, ISurrealCommand)` helpers de
  ejecución (relanzado desde Unreleased previa).

### Changed
- `SurrealTransactionBuilder.RebaseSql` ya no usa regex: ahora hace
  `string.Replace` sobre placeholders conocidos (vía la nueva
  `ISurrealCommand.Placeholders`). Elimina el bug de literales con `$`.
- `ParameterBag.Snapshot()` devuelve copia defensiva (`new Dictionary<>(...)`)
  en lugar de la referencia viva. `Build()` ahora es verdaderamente
  inmutable: añadir más parámetros al bag después de `Build()` no muta el
  comando ya construido.
- `SurrealCommand` ahora acepta un tercer parámetro `IReadOnlyList<string>
  Placeholders`. El ctor binario legacy se mantiene para `SurrealQuery.Raw`
  y `SurrealQuery.Kill` (no rompe implementaciones externas de
  `ISurrealCommand` gracias al default `Array.Empty<string>`).

### Fixed
- **Bug silencioso en `ExecuteNoResultAsync` y `InsertWithIdAsync`**: la
  reflexión agarraba la propiedad equivocada del error SurrealDb.Net.
  `RpcErrorResponseContent.Details` es un objeto complejo
  (`RpcErrorDetails`), no el mensaje — el texto vive en
  `RpcErrorResponseContent.Message`. Como consecuencia, el check de
  "Transaction conflict" **nunca matcheaba**: una respuesta con
  Transaction conflict se reportaba como `InvalidOperationException("")`
  en vez de no-op. El nuevo `SurrealDbErrorExtractor` prueba `Message`
  primero, `Details` después (en caso de que `Details` sea string en
  otras shapes del SDK), y `ToString` al final.
- **Bug de corrupción de SQL en transactions**: `$word` dentro de literales
  string (p.e. `'price: $5.00'`) o comentarios se reescribía como
  `$s0_5.00`, produciendo SQL inválido. Ahora el rebase sólo opera sobre
  placeholders conocidos, no sobre cualquier `$word`.
- `ParameterBag.Snapshot()` ya no muta snapshots ya entregados a un
  `ISurrealCommand` cuando se siguen añadiendo parámetros al bag.
- `InsertWithIdAsync` valida ahora `null`/empty en `table`, `fields` e
  `idGenerator`. Antes, una llamada con `table=null` reventaba con
  `NullReferenceException` en `RecordId.From(null, ...)` sin contexto.
- `InsertWithIdAsync` valida que `idGenerator()` no devuelva string vacío.
  Antes producía `table:` que SurrealDB rechazaba con un error de parse.

### Notes
- **Icono del paquete NuGet** pospuesto a 0.5.0. Falta el PNG físico en
  `assets/icon.png` y el upload se hace desde fuera del chat.

[Unreleased]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.7.0...HEAD
[0.7.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/pierocarrion/SurrealDb.Net.Linq/compare/v0.3.4...v0.4.0
- `SurrealQuery.BeginTransaction()` and `SurrealTransactionBuilder`: build multi-statement `BEGIN; … COMMIT;` / `BEGIN; … CANCEL;` transactions from existing `ISurrealCommand` instances. Parameter names are automatically rebased per statement (`$s0_p0`, `$s1_p0`, …) to avoid collisions.
- `ExecuteCountAsync(this ISurrealDbClient, ISurrealCommand)` and `ExecuteAnyAsync<T>(this ISurrealDbClient, ISurrealCommand)` execution helpers.

### Changed
- Test project now multi-targets `net6.0` and `net8.0` to exercise the `netstandard2.1` library asset on an older runtime.

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
