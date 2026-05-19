using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class MemberNameResolverTests
{
    [Theory]
    [InlineData("Email", "email")]
    [InlineData("HireDate", "hire_date")]
    [InlineData("CreatedAtUtc", "created_at_utc")]
    [InlineData("Id", "id")]
    [InlineData("X", "x")]
    [InlineData("", "")]
    public void ToSnakeCase_lowercases_and_inserts_underscores_before_uppercase_after_first(string input, string expected)
    {
        Assert.Equal(expected, MemberNameResolver.ToSnakeCase(input));
    }

    [Fact]
    public void Resolve_honors_JsonPropertyName_when_present()
    {
        var member = typeof(User).GetProperty(nameof(User.HireDate))!;

        Assert.Equal("hire_date_custom", MemberNameResolver.Resolve(member));
    }

    [Fact]
    public void Resolve_falls_back_to_snake_case_when_no_attribute()
    {
        var member = typeof(User).GetProperty(nameof(User.CreatedAt))!;

        Assert.Equal("created_at", MemberNameResolver.Resolve(member));
    }
}
