#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill for C# 9+ record types on .NET Standard 2.1. The compiler emits
/// this type when compiling positional records, but it is not part of the
/// .NET Standard 2.1 reference assemblies.
/// </summary>
internal static class IsExternalInit
{
}
#endif
