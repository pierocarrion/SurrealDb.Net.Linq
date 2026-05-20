using Dahomey.Cbor;

namespace SurrealDb.Net.Linq.Cbor;

/// <summary>
/// Hooks that restore SurrealDB-friendly CBOR defaults that the official
/// <c>SurrealDb.Net</c> package dropped in 0.10.x (where the built-in naming
/// convention now returns <c>member.Name</c> verbatim).
///
/// <para>Plug into <c>SurrealDb.Net</c> at construction time:</para>
/// <code>
/// var client = new SurrealDbClient(
///     options,
///     configureCborOptions: opts => opts.UseSurrealSnakeCase());
/// </code>
/// </summary>
public static class SurrealCborOptionsExtensions
{
    /// <summary>
    /// Restores the snake_case naming convention the vendored fork of
    /// <c>SurrealDb.Net</c> shipped. Apply this on every
    /// <see cref="CborOptions"/> instance you create via SurrealDb.Net's
    /// <c>configureCborOptions</c> hook so typed <c>Select&lt;T&gt;(table)</c>,
    /// <c>Select&lt;T&gt;(RecordId)</c>, and <c>Create&lt;T&gt;</c> calls
    /// round-trip against snake_case SurrealDB schemas.
    /// </summary>
    /// <remarks>
    /// Does NOT touch the global <see cref="Dictionary{TKey, TValue}"/>
    /// converter — overriding it would break <c>RawQuery</c> parameter
    /// serialization. If you need a dynamic-shape <c>Dictionary&lt;string,
    /// object?&gt;</c> row field, opt in per-field with
    /// <c>[CborConverter(typeof(CborMapToDictionaryConverter))]</c>.
    /// </remarks>
    /// <param name="options">CBOR options instance (typically the one passed
    /// to your <c>SurrealDbClient</c> via <c>configureCborOptions</c>).</param>
    /// <returns>The same instance, to allow fluent chaining.</returns>
    public static CborOptions UseSurrealSnakeCase(this CborOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        options.DefaultNamingConvention = SnakeCaseCborNamingConvention.Instance;
        return options;
    }
}
