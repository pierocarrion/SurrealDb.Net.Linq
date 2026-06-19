using System.Text;
using System.Text.RegularExpressions;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Fluent builder for SurrealDB multi-statement transactions.
/// Collects one or more <see cref="ISurrealCommand"/> statements, rewrites
/// parameter names so they do not collide across statements, and emits a single
/// <c>BEGIN; … COMMIT;</c> or <c>BEGIN; … CANCEL;</c> command.
/// </summary>
public sealed class SurrealTransactionBuilder
{
    private static readonly Regex ParameterRegex = new(
        @"\$(?<name>\w+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        sb.AppendLine("BEGIN;");

        for (var i = 0; i < _commands.Count; i++)
        {
            var command = _commands[i];
            var prefix = $"s{i}_";

            var nameMap = command.Parameters.Keys.ToDictionary(
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
        }

        sb.Append(_commit.Value ? "COMMIT;" : "CANCEL;");

        return new SurrealCommand(sb.ToString(), combinedParams);
    }

    private static string RebaseSql(string sql, IReadOnlyDictionary<string, string> nameMap)
    {
        return ParameterRegex.Replace(sql, m =>
        {
            var name = m.Groups["name"].Value;
            if (nameMap.TryGetValue(name, out var newName))
            {
                return $"${newName}";
            }
            return m.Value;
        });
    }
}
