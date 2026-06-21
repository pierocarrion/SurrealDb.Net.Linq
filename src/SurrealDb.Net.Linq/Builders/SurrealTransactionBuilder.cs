using System.Text;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Fluent builder for SurrealDB multi-statement transactions.
/// Collects one or more <see cref="ISurrealCommand"/> statements, rewrites
/// parameter names so they do not collide across statements, and emits a single
/// <c>BEGIN; … COMMIT;</c> or <c>BEGIN; … CANCEL;</c> command.
/// </summary>
public sealed class SurrealTransactionBuilder
{
    private readonly List<ISurrealCommand> _commands = new();
    private bool? _commit;

    internal SurrealTransactionBuilder()
    {
    }

    /// <summary>Add a statement to the transaction.</summary>
    public SurrealTransactionBuilder Add(ISurrealCommand command)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        _commands.Add(command);
        return this;
    }

    /// <summary>Close the transaction with <c>COMMIT</c>.</summary>
    public SurrealTransactionBuilder Commit()
    {
        _commit = true;
        return this;
    }

    /// <summary>Close the transaction with <c>CANCEL</c> (rollback).</summary>
    public SurrealTransactionBuilder Rollback()
    {
        _commit = false;
        return this;
    }

    /// <summary>
    /// Build the combined transaction command. Parameter names from each added
    /// command are prefixed with <c>s{i}_</c> so that <c>$p0</c> in statement 0
    /// and <c>$p0</c> in statement 1 become <c>$s0_p0</c> and <c>$s1_p0</c>.
    /// </summary>
    public ISurrealCommand Build()
    {
        if (_commands.Count == 0)
        {
            throw new InvalidOperationException("A transaction requires at least one statement.");
        }
        if (_commit is null)
        {
            throw new InvalidOperationException("Call Commit() or Rollback() before Build().");
        }

        var sb = new StringBuilder();
        var combinedParams = new Dictionary<string, object?>();
        var combinedPlaceholders = new List<string>();

        sb.AppendLine("BEGIN;");

        for (var i = 0; i < _commands.Count; i++)
        {
            var command = _commands[i];
            var prefix = $"s{i}_";

            // Usar Placeholders conocidos si el comando los expone; si no,
            // caer a las keys de Parameters (comportamiento legacy para
            // comandos construidos fuera de los builders, p.e. Raw/Kill).
            var sourcePlaceholders = command.Placeholders.Count > 0
                ? command.Placeholders
                : command.Parameters.Keys.ToList();

            var nameMap = sourcePlaceholders.ToDictionary(
                name => name,
                name => $"{prefix}{name}");

            var rebasedSql = RebaseSql(command.Sql, nameMap);
            sb.Append(rebasedSql);
            if (!rebasedSql.TrimEnd().EndsWith(";", StringComparison.Ordinal))
            {
                sb.Append(';');
            }
            sb.AppendLine();

            foreach (var kv in command.Parameters)
            {
                combinedParams[nameMap[kv.Key]] = kv.Value;
            }
            foreach (var name in sourcePlaceholders)
            {
                combinedPlaceholders.Add(nameMap[name]);
            }
        }

        sb.Append(_commit.Value ? "COMMIT;" : "CANCEL;");

        return new SurrealCommand(sb.ToString(), combinedParams, combinedPlaceholders);
    }

    /// <summary>
    /// Reemplazo textual y seguro: sólo operamos sobre placeholders conocidos
    /// (con prefijo <c>$</c>), no sobre cualquier <c>$word</c> en el SQL.
    /// Esto arregla el bug donde literales como <c>'price: $5.00'</c> dentro
    /// de un <see cref="SurrealQuery.Raw(string, IDictionary{string, object?}?)"/>
    /// resultaban corruptos al reescribirse como <c>$s0_5.00</c>.
    /// </summary>
    private static string RebaseSql(string sql, IReadOnlyDictionary<string, string> nameMap)
    {
        if (nameMap.Count == 0) return sql;
        var result = sql;
        foreach (var kv in nameMap)
        {
            var oldToken = "$" + kv.Key;
            var newToken = "$" + kv.Value;
            result = result.Replace(oldToken, newToken, StringComparison.Ordinal);
        }
        return result;
    }
}

