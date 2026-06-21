namespace SurrealDb.Net.Linq;

/// <summary>
/// Operadores de comparación soportados por la API fluida
/// <c>Where(field, SurrealOperator, value)</c>. Mappean directamente a la
/// sintaxis SurrealQL.
/// </summary>
public enum SurrealOperator
{
    Equals,
    NotEquals,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    In,
    NotIn,
    Contains,
    ContainsNot,
    Inside,
    /// <summary>Operador geométrico — el campo está fuera del área dada.</summary>
    Outside,
    /// <summary>Operador geométrico — el campo intersecta el área dada.</summary>
    Intersects,
    /// <summary>Todos los elementos del campo están dentro de la colección dada.</summary>
    AllInside,
    /// <summary>Algún elemento del campo está dentro de la colección dada.</summary>
    AnyInside,
    /// <summary>Todos los elementos del campo están fuera de la colección dada.</summary>
    AllOutside,
    /// <summary>Algún elemento del campo está fuera de la colección dada.</summary>
    AnyOutside,
    /// <summary>
    /// Match de regex (<c>field ~ $p</c>). Renombrado en 0.6.0 para
    /// representar el operador regex de SurrealDB (antes, <see cref="Like"/>
    /// emitía <c>string::contains</c>).
    /// </summary>
    Matches,
    /// <summary>Match negado de regex (<c>field !~ $p</c>).</summary>
    NotMatches,
    /// <summary>
    /// Equivalente a <c>string::contains(field, $p)</c>. Mantenido por
    /// retrocompatibilidad; considerar usar <see cref="Matches"/> para
    /// regex nativo de SurrealDB.
    /// </summary>
    Like,
    IsNone,
    IsNotNone,
}
