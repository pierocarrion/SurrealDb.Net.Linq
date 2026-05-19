using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Maps a CLR <see cref="MemberInfo"/> to a SurrealQL field name.
/// Convention: <c>[JsonPropertyName("...")]</c> wins; otherwise the PascalCase
/// property name is lowered with underscores inserted before each uppercase
/// letter after the first (<c>HireDate</c> → <c>hire_date</c>).
/// </summary>
internal static class MemberNameResolver
{
    private static readonly ConcurrentDictionary<MemberInfo, string> Cache = new();

    public static string Resolve(MemberInfo member) =>
        Cache.GetOrAdd(member, static m =>
        {
            var attr = m.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (attr is not null && !string.IsNullOrWhiteSpace(attr.Name))
            {
                return attr.Name;
            }
            return ToSnakeCase(m.Name);
        });

    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
