// Polyfill: CallerArgumentExpression se añadió en C# 10 / .NET 6.
// En netstandard2.1 el atributo no existe; lo definimos aquí. El parámetro
// opcional en Arg.NotNull sigue funcionando (queda null en runtime).
#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
internal sealed class CallerArgumentExpressionAttribute : Attribute
{
    public CallerArgumentExpressionAttribute(string name) => Name = name;
    public string Name { get; }
}
#endif
