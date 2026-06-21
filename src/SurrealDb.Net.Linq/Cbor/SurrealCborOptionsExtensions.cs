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
///
/// <para>Since 0.7.0 each converter is also available as an individual
/// opt-in (<see cref="UseSnakeCaseNaming"/>,
/// <see cref="UseDateTimeOffsetConverter"/>,
/// <see cref="UseMapToDictionaryConverter"/>) for setups where only one of
/// the three is needed.</para>
/// </summary>
public static class SurrealCborOptionsExtensions
{
    /// <summary>
    /// Registers <see cref="SnakeCaseCborNamingConvention"/> as the default
    /// naming convention — maps CLR <c>PascalCase</c> member names to
    /// SurrealDB <c>snake_case</c> columns (<c>HireDate</c> →
    /// <c>hire_date</c>). Honors <c>[Column("name")]</c> overrides.
    /// </summary>
    public static CborOptions UseSnakeCaseNaming(this CborOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        options.DefaultNamingConvention = SnakeCaseCborNamingConvention.Instance;
        return options;
    }

    /// <summary>
    /// Registers <see cref="DateTimeOffsetConverter"/> for
    /// <see cref="DateTimeOffset"/> against SurrealDB's
    /// <c>tag(12) [seconds, nanos]</c> wire shape. The official
    /// <c>SurrealDb.Net</c> package only registers a converter for
    /// <see cref="DateTime"/>, leaving <see cref="DateTimeOffset"/> to fall
    /// back to <c>ObjectConverter&lt;DateTimeOffset&gt;</c> which explodes
    /// with <c>"Expected major type Map (5)"</c> on the first non-null row.
    /// </summary>
    public static CborOptions UseDateTimeOffsetConverter(this CborOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        options.Registry.ConverterRegistry.RegisterConverter(
            typeof(DateTimeOffset),
            new DateTimeOffsetConverter());
        return options;
    }

    /// <summary>
    /// Registers <see cref="CborMapToDictionaryConverter"/> for
    /// <see cref="Dictionary{TKey, TValue}"/> (<c>string</c> →
    /// <c>object?</c>). Required because Dahomey.Cbor 1.26.1's stock
    /// <c>DictionaryConverter</c> resolves the value converter to
    /// <c>ObjectConverter&lt;object&gt;</c> at compile-time, which expects a
    /// CBOR Map for every value — so rows with <c>object FLEXIBLE</c>
    /// columns carrying mixed primitives explode on read.
    /// </summary>
    public static CborOptions UseMapToDictionaryConverter(this CborOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        options.Registry.ConverterRegistry.RegisterConverter(
            typeof(Dictionary<string, object?>),
            new CborMapToDictionaryConverter(options));
        return options;
    }

    /// <summary>
    /// Composite that applies all three converters:
    /// <see cref="UseSnakeCaseNaming"/> +
    /// <see cref="UseDateTimeOffsetConverter"/> +
    /// <see cref="UseMapToDictionaryConverter"/>.
    /// </summary>
    public static CborOptions UseSurrealSnakeCase(this CborOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        return options
            .UseSnakeCaseNaming()
            .UseDateTimeOffsetConverter()
            .UseMapToDictionaryConverter();
    }
}

