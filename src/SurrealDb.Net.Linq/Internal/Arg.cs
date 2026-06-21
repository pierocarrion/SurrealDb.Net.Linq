namespace SurrealDb.Net.Linq;

/// <summary>
/// Helpers de validación de argumentos con polyfill compile-time para
/// netstandard2.1 (donde <c>ArgumentNullException.ThrowIfNull</c> no existe).
/// En .NET 6+ el atributo <c>[CallerArgumentExpression]</c> captura el nombre
/// del parámetro automáticamente; en netstandard2.1 queda <c>null</c> y el
/// mensaje del ArgumentNullException no incluye el nombre — tradeo aceptable.
/// </summary>
internal static class Arg
{
    public static T NotNull<T>(T? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
    {
        if (value is null) throw new ArgumentNullException(name);
        return value;
    }

    public static void NotNullOrWhiteSpace(string? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value must be non-empty.", name);
    }

    public static void NonNegative(int value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(name, value, "Value must be non-negative.");
    }
}
