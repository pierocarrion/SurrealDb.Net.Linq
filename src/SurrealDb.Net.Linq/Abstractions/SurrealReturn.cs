namespace SurrealDb.Net.Linq;

/// <summary>
/// Modo de retorno para sentencias CREATE / UPDATE / UPSERT / DELETE.
/// Reemplaza los literales "BEFORE" / "AFTER" / "NONE" / "DIFF" internos y
/// expone una superficie tipada. Para retorno de campos concretos, usa el
/// overload <c>ReturnFields(params string[])</c> en el builder.
/// </summary>
public enum SurrealReturn
{
    /// <summary>RETURN NONE — no devolver filas (default para CREATE con id autogenerado).</summary>
    None,
    /// <summary>RETURN BEFORE — estado previo a la mutación.</summary>
    Before,
    /// <summary>RETURN AFTER — estado posterior a la mutación.</summary>
    After,
    /// <summary>RETURN DIFF — diff de los cambios.</summary>
    Diff,
}
