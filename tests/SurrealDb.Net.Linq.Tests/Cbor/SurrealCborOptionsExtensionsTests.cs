using Dahomey.Cbor;
using SurrealDb.Net.Linq.Cbor;
using Xunit;

namespace SurrealDb.Net.Linq.Tests.Cbor;

public class SurrealCborOptionsExtensionsTests
{
    [Fact]
    public void UseSurrealSnakeCase_sets_snake_case_naming_convention()
    {
        var options = new CborOptions();
        options.UseSurrealSnakeCase();

        Assert.NotNull(options.DefaultNamingConvention);
        Assert.Contains("SnakeCase", options.DefaultNamingConvention!.GetType().Name);
    }

    [Fact]
    public void UseSnakeCaseNaming_only_sets_naming_convention()
    {
        var options = new CborOptions();
        options.UseSnakeCaseNaming();

        Assert.NotNull(options.DefaultNamingConvention);
        Assert.Contains("SnakeCase", options.DefaultNamingConvention!.GetType().Name);
    }

    [Fact]
    public void UseDateTimeOffsetConverter_registers_converter_for_DateTimeOffset()
    {
        var options = new CborOptions();
        options.UseDateTimeOffsetConverter();

        var converter = options.Registry.ConverterRegistry.Lookup(typeof(DateTimeOffset));
        Assert.NotNull(converter);
    }

    [Fact]
    public void UseMapToDictionaryConverter_registers_converter_for_Dictionary()
    {
        var options = new CborOptions();
        options.UseMapToDictionaryConverter();

        var converter = options.Registry.ConverterRegistry.Lookup(typeof(Dictionary<string, object?>));
        Assert.NotNull(converter);
    }

    [Fact]
    public void Composite_is_equivalent_to_individual_opt_ins()
    {
        var composite = new CborOptions().UseSurrealSnakeCase();

        var individual = new CborOptions()
            .UseSnakeCaseNaming()
            .UseDateTimeOffsetConverter()
            .UseMapToDictionaryConverter();

        // Naming convention
        Assert.Equal(composite.DefaultNamingConvention!.GetType(),
                    individual.DefaultNamingConvention!.GetType());
        // DateTimeOffset converter
        Assert.NotNull(individual.Registry.ConverterRegistry.Lookup(typeof(DateTimeOffset)));
        // Map converter
        Assert.NotNull(individual.Registry.ConverterRegistry.Lookup(typeof(Dictionary<string, object?>)));
    }

    [Fact]
    public void All_methods_throw_on_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => ((CborOptions)null!).UseSurrealSnakeCase());
        Assert.Throws<ArgumentNullException>(() => ((CborOptions)null!).UseSnakeCaseNaming());
        Assert.Throws<ArgumentNullException>(() => ((CborOptions)null!).UseDateTimeOffsetConverter());
        Assert.Throws<ArgumentNullException>(() => ((CborOptions)null!).UseMapToDictionaryConverter());
    }
}
