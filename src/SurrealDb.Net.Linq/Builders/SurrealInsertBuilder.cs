using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Fluent builder for <c>INSERT INTO &lt;target&gt; [ (field, …) ] VALUES
/// (…), (…), … [ ON DUPLICATE KEY UPDATE … ] [ RETURN … ]</c>.
/// El DML <c>INSERT</c> que faltaba en la lib — útil para bulk-load de
/// filas y para <c>UPSERT</c>-style semantics con <c>ON DUPLICATE KEY UPDATE</c>.
/// </summary>
public sealed class SurrealInsertBuilder
{
    private readonly string _target;
    private readonly ParameterBag _bag = new();
    private readonly List<string> _columns = new();
    private readonly List<List<string>> _valuePlaceholders = new();
    private readonly List<string> _onDuplicateKeyUpdate = new();
    private string? _returnClause;

    internal SurrealInsertBuilder(string target)
    {
        Arg.NotNullOrWhiteSpace(target);
        _target = target;
    }

    /// <summary>
    /// Declara las columnas a insertar. Requiere una llamada posterior a
    /// <see cref="Values"/> por cada fila con el mismo número de valores.
    /// Si no se llama, INSERT INTO genera <c>(field1, …) VALUES (val1, …)</c>
    /// sin nombres de columna explícitos.
    /// </summary>
    public SurrealInsertBuilder Columns(params string[] columns)
    {
        if (columns is null || columns.Length == 0)
            throw new ArgumentException("Columns requires at least one field.", nameof(columns));
        foreach (var c in columns) Arg.NotNullOrWhiteSpace(c);
        _columns.Clear();
        _columns.AddRange(columns);
        return this;
    }

    /// <summary>
    /// Añade una fila de valores (cada valor se parametriza). El número de
    /// valores debe matchear el número de columnas declaradas con
    /// <see cref="Columns"/> (si se llamó).
    /// </summary>
    public SurrealInsertBuilder Values(params object?[] values)
    {
        if (values is null || values.Length == 0)
            throw new ArgumentException("Values requires at least one value.", nameof(values));
        if (_columns.Count > 0 && values.Length != _columns.Count)
            throw new ArgumentException(
                $"Values count ({values.Length}) must match Columns count ({_columns.Count}).",
                nameof(values));

        var row = new List<string>(values.Length);
        foreach (var v in values) row.Add(_bag.Add(v));
        _valuePlaceholders.Add(row);
        return this;
    }

    /// <summary>
    /// Añade una cláusula <c>ON DUPLICATE KEY UPDATE field = expr [, …]</c>.
    /// Si el campo ya existe con un UNIQUE constraint, ejecuta el update en
    /// vez de fallar. La expr puede ser un placeholder parametrizado vía
    /// <see cref="Bind"/> o un literal SurrealQL.
    /// </summary>
    public SurrealInsertBuilder OnDuplicateKeyUpdate(string field, string expression)
    {
        Arg.NotNullOrWhiteSpace(field);
        Arg.NotNullOrWhiteSpace(expression);
        _onDuplicateKeyUpdate.Add($"{field} = {expression}");
        return this;
    }

    /// <summary>Bulk parameter binding.</summary>
    public SurrealInsertBuilder Bind(string name, object? value)
    {
        Arg.NotNullOrWhiteSpace(name);
        _bag.AddNamed(name, value);
        return this;
    }

    public SurrealInsertBuilder ReturnBefore() { _returnClause = "BEFORE"; return this; }
    public SurrealInsertBuilder ReturnAfter() { _returnClause = "AFTER"; return this; }
    public SurrealInsertBuilder ReturnNone() { _returnClause = "NONE"; return this; }
    public SurrealInsertBuilder ReturnDiff() { _returnClause = "DIFF"; return this; }
    public SurrealInsertBuilder Return(string expression)
    {
        Arg.NotNullOrWhiteSpace(expression);
        _returnClause = expression;
        return this;
    }
    public SurrealInsertBuilder Return(SurrealReturn value)
    {
        _returnClause = SurrealReturnRenderer.Render(value);
        return this;
    }
    public SurrealInsertBuilder ReturnFields(params string[] fields)
    {
        if (fields is null || fields.Length == 0)
            throw new ArgumentException("ReturnFields requires at least one field.", nameof(fields));
        foreach (var f in fields) Arg.NotNullOrWhiteSpace(f);
        _returnClause = string.Join(", ", fields);
        return this;
    }

    public ISurrealCommand Build()
    {
        if (_valuePlaceholders.Count == 0)
            throw new InvalidOperationException("INSERT requires at least one Values() row.");

        var sb = new StringBuilder();
        sb.Append("INSERT INTO ").Append(_target);

        if (_columns.Count > 0)
        {
            sb.Append(" (").Append(string.Join(", ", _columns)).Append(')');
        }

        sb.Append(" VALUES ");
        for (var i = 0; i < _valuePlaceholders.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('(').Append(string.Join(", ", _valuePlaceholders[i])).Append(')');
        }

        if (_onDuplicateKeyUpdate.Count > 0)
        {
            sb.Append(" ON DUPLICATE KEY UPDATE ")
              .Append(string.Join(", ", _onDuplicateKeyUpdate));
        }

        if (_returnClause is not null) sb.Append(" RETURN ").Append(_returnClause);

        return new SurrealCommand(sb.ToString(), _bag.Snapshot(), _bag.GetPlaceholders());
    }
}
