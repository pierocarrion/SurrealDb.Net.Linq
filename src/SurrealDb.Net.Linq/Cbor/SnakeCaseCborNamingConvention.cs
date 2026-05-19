using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;
using Dahomey.Cbor.Serialization.Conventions;

namespace SurrealDb.Net.Linq.Cbor;

/// <summary>
/// Dahomey.Cbor <see cref="INamingConvention"/> that maps CLR member names to
/// SurrealDB snake_case field names — restoring the behavior the vendored fork
/// of <c>SurrealDb.Net</c> shipped before the official package switched to
/// returning <see cref="MemberInfo.Name"/> verbatim.
///
/// <para>Resolution order:</para>
/// <list type="number">
///   <item><description><c>[Column("name")]</c> wins (already canonical wire name).</description></item>
///   <item><description>Otherwise, the CLR name is converted to snake_case:
///     a <c>_</c> is inserted before each uppercase letter that isn't already
///     preceded by <c>_</c>, then the whole string is lowercased.</description></item>
/// </list>
///
/// <para>Examples:</para>
/// <list type="bullet">
///   <item><description><c>HireDate</c> → <c>hire_date</c></description></item>
///   <item><description><c>User_Id</c> → <c>user_id</c> (existing underscore preserved, no double)</description></item>
///   <item><description><c>UserAgent</c> → <c>user_agent</c></description></item>
///   <item><description><c>logo_r2_key</c> → <c>logo_r2_key</c> (already snake_case, unchanged)</description></item>
/// </list>
/// </summary>
public sealed class SnakeCaseCborNamingConvention : INamingConvention
{
    /// <summary>
    /// Singleton instance — the convention is stateless and safe to reuse
    /// across <see cref="Dahomey.Cbor.CborOptions"/> instances.
    /// </summary>
    public static readonly SnakeCaseCborNamingConvention Instance = new();

    /// <inheritdoc />
    public string GetPropertyName(MemberInfo member)
    {
        var columnAttribute = member.GetCustomAttribute<ColumnAttribute>();
        if (columnAttribute is not null && !string.IsNullOrEmpty(columnAttribute.Name))
        {
            return columnAttribute.Name;
        }
        return ToSnakeCase(member.Name);
    }

    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_')
            {
                sb.Append('_');
                continue;
            }
            if (char.IsUpper(c))
            {
                if (i > 0 && name[i - 1] != '_')
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
