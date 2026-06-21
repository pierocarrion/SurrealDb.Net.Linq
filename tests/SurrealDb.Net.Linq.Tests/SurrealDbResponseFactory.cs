using System.Reflection;
using SurrealDb.Net.Models.Response;

namespace SurrealDb.Net.Linq.Tests;

/// <summary>
/// Helper para construir <see cref="SurrealDbResponse"/> en tests sin
/// depender de tipos internos del SDK. Usa reflexión sobre los ctors
/// internal de <see cref="SurrealDbErrorResult"/> / <see cref="SurrealDbOkResult"/>.
/// Si el SDK rename los ctors, los tests fallan ruidosamente — preferimos
/// eso a tests verdes que no prueban nada.
/// </summary>
internal static class SurrealDbResponseFactory
{
    private static readonly Type? ErrorResultType =
        typeof(SurrealDbResponse).Assembly.GetType("SurrealDb.Net.Models.Response.SurrealDbErrorResult");

    /// <summary>
    /// Construye una respuesta con un único resultado de error. Cubre el
    /// path HasErrors=true de las extensiones *Strict y de NoResult.
    /// </summary>
    public static SurrealDbResponse WithError(string details, string status = "ERR")
    {
        if (ErrorResultType is null)
            throw new InvalidOperationException("SurrealDbErrorResult type not found — SDK changed shape.");

        // internal ctor: (RpcErrorKind kind, TimeSpan time, String status, String details)
        var kind = ErrorResultType.Assembly.GetType("SurrealDb.Net.Models.Errors.RpcErrorKind");
        object? kindDefault = kind is not null ? Activator.CreateInstance(kind) : null;
        var ctorArgs = new object?[]
        {
            kindDefault,        // RpcErrorKind (record/class with default ctor)
            TimeSpan.Zero,      // time
            status,             // status
            details              // details (string — donde vive el texto real)
        };
        var ctorArgTypes = new[]
        {
            kind ?? typeof(object),
            typeof(TimeSpan),
            typeof(string),
            typeof(string)
        };
        var ctor = ErrorResultType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            ctorArgTypes,
            null)
            ?? throw new InvalidOperationException(
                $"SurrealDbErrorResult internal ctor not found. SDK shape changed.");

        var errorResult = ctor.Invoke(ctorArgs);
        var list = new List<ISurrealDbResult> { (ISurrealDbResult)errorResult };
        return new SurrealDbResponse(list);
    }

    /// <summary>Respuesta vacía (sin resultados) — HasErrors=false, GetValue devuelve default.</summary>
    public static SurrealDbResponse Empty() => new(new List<ISurrealDbResult>());
}
