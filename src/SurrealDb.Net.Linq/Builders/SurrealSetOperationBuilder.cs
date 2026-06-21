using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Tipo de operación de conjuntos para <see cref="SurrealSetOperationBuilder"/>.
/// </summary>
public enum SurrealSetOperationKind
{
    /// <summary>UNION — combina filas duplicadas.</summary>
    Union,
    /// <summary>UNION ALL — incluye duplicados.</summary>
    UnionAll,
    /// <summary>INTERSECT — filas presentes en ambas queries.</summary>
    Intersect,
    /// <summary>DIFFERENCE — filas en la primera pero no en la segunda.</summary>
    Difference,
}

/// <summary>
/// Builder composicional para operaciones de conjuntos sobre N
/// <see cref="ISurrealCommand"/> (típicamente SELECTs). Combina las queries
/// con UNION / UNION ALL / INTERSECT / DIFFERENCE y reescribe parámetros
/// con el mismo esquema prefix-everything del transaction builder.
/// </summary>
public sealed class SurrealSetOperationBuilder
{
    private readonly List<ISurrealCommand> _commands = new();
    private readonly List<SurrealSetOperationKind> _operators = new();

    internal SurrealSetOperationBuilder() { }

    /// <summary>Primer operand de la operación.</summary>
    public SurrealSetOperationBuilder From(ISurrealCommand first)
    {
        if (first is null) throw new ArgumentNullException(nameof(first));
        if (_commands.Count > 0)
            throw new InvalidOperationException("From() must be called exactly once as the first operand.");
        _commands.Add(first);
        return this;
    }

    /// <summary>Combina con el siguiente operand usando UNION.</summary>
    public SurrealSetOperationBuilder Union(ISurrealCommand next) => Add(next, SurrealSetOperationKind.Union);

    /// <summary>Combina con el siguiente operand usando UNION ALL.</summary>
    public SurrealSetOperationBuilder UnionAll(ISurrealCommand next) => Add(next, SurrealSetOperationKind.UnionAll);

    /// <summary>Combina con el siguiente operand usando INTERSECT.</summary>
    public SurrealSetOperationBuilder Intersect(ISurrealCommand next) => Add(next, SurrealSetOperationKind.Intersect);

    /// <summary>Combina con el siguiente operand usando DIFFERENCE.</summary>
    public SurrealSetOperationBuilder Difference(ISurrealCommand next) => Add(next, SurrealSetOperationKind.Difference);

    private SurrealSetOperationBuilder Add(ISurrealCommand next, SurrealSetOperationKind kind)
    {
        if (next is null) throw new ArgumentNullException(nameof(next));
        if (_commands.Count == 0)
            throw new InvalidOperationException("Call From() first before Union/Intersect/Difference.");
        _commands.Add(next);
        _operators.Add(kind);
        return this;
    }

    /// <summary>Build the combined statement.</summary>
    public ISurrealCommand Build()
    {
        if (_commands.Count < 2)
            throw new InvalidOperationException("Set operation requires at least two operands (From + one Union/Intersect/Difference).");

        var sb = new StringBuilder();
        var combinedParams = new Dictionary<string, object?>();
        var combinedPlaceholders = new List<string>();

        for (var i = 0; i < _commands.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ').Append(RenderKind(_operators[i - 1])).Append(' ');
            }
            var cmd = _commands[i];
            var prefix = $"o{i}_";
            var placeholders = cmd.Placeholders.Count > 0
                ? cmd.Placeholders
                : (IReadOnlyList<string>)cmd.Parameters.Keys.ToList();
            var nameMap = placeholders.ToDictionary(n => n, n => $"{prefix}{n}");
            var rebasedSql = RebaseSql(cmd.Sql, nameMap);
            sb.Append('(').Append(rebasedSql).Append(')');
            foreach (var kv in cmd.Parameters) combinedParams[nameMap[kv.Key]] = kv.Value;
            foreach (var n in placeholders) combinedPlaceholders.Add(nameMap[n]);
        }

        return new SurrealCommand(sb.ToString(), combinedParams, combinedPlaceholders);
    }

    private static string RenderKind(SurrealSetOperationKind kind) => kind switch
    {
        SurrealSetOperationKind.Union => "UNION",
        SurrealSetOperationKind.UnionAll => "UNION ALL",
        SurrealSetOperationKind.Intersect => "INTERSECT",
        SurrealSetOperationKind.Difference => "DIFFERENCE",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown set operation kind."),
    };

    private static string RebaseSql(string sql, IReadOnlyDictionary<string, string> nameMap)
    {
        if (nameMap.Count == 0) return sql;
        var result = sql;
        foreach (var kv in nameMap)
        {
            result = result.Replace("$" + kv.Key, "$" + kv.Value, StringComparison.Ordinal);
        }
        return result;
    }
}
