using Dahomey.Cbor;

namespace SurrealDb.Net.Linq.Cbor;

/// <summary>
/// Hooks that restore SurrealDB-friendly CBOR defaults that the official
/// <c>SurrealDb.Net</c> package dropped in 0.10.x (or never shipped at all).
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
    /// Applies the SurrealDB-friendly CBOR defaults this library carries:
    /// <list type="bullet">
    ///   <item><description>snake_case <see cref="Dahomey.Cbor.Serialization.Conventions.INamingConvention"/> —
    ///   the vendored fork shipped it, the official package dropped it in
    ///   0.10.x and now returns <c>member.Name</c> verbatim.</description></item>
    ///   <item><description>A converter for <see cref="DateTimeOffset"/> against SurrealDB's
    ///   <c>tag(12) [seconds, nanos]</c> wire shape — the official package
    ///   only registers one for <see cref="DateTime"/>, leaving
    ///   <see cref="DateTimeOffset"/> to fall back to
    ///   <c>ObjectConverter&lt;DateTimeOffset&gt;</c> which explodes with
    ///   <c>"Expected major type Map (5)"</c> on the first non-null row.</description></item>
    ///   <item><description>A converter for <see cref="Dictionary{TKey, TValue}"/>
    ///   (<c>string</c> → <c>object?</c>) — Dahomey.Cbor 1.26.1's stock
    ///   <c>DictionaryConverter</c> resolves the value converter to
    ///   <c>ObjectConverter&lt;object&gt;</c> at compile-time, which expects
    ///   a CBOR Map for every value, so rows with <c>object FLEXIBLE</c>
    ///   columns carrying mixed primitives explode on read. The converter
    ///   handles both Read (any CBOR value → boxed CLR value) and Write
    ///   (dispatches by runtime type) so it doesn't break
    ///   <c>RawQuery</c> parameter serialization either.</description></item>
    /// </list>
    /// </summary>
    /// <param name="options">CBOR options instance (typically the one passed
    /// to your <c>SurrealDbClient</c> via <c>configureCborOptions</c>).</param>
    /// <returns>The same instance, to allow fluent chaining.</returns>
    public static CborOptions UseSurrealSnakeCase(this CborOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        options.DefaultNamingConvention = SnakeCaseCborNamingConvention.Instance;
        options.Registry.ConverterRegistry.RegisterConverter(
            typeof(DateTimeOffset),
            new DateTimeOffsetConverter());
        options.Registry.ConverterRegistry.RegisterConverter(
            typeof(Dictionary<string, object?>),
            new CborMapToDictionaryConverter(options));

        return options;
    }
}
