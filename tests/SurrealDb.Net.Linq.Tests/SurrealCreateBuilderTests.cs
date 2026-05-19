using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealCreateBuilderTests
{
    [Fact]
    public void Set_emits_field_eq_placeholder_per_call()
    {
        var cmd = SurrealQuery.Create("user")
            .Set("email", "bob@example.com")
            .Set("active", true)
            .Build();

        Assert.Equal("CREATE user SET email = $p0, active = $p1", cmd.Sql);
        Assert.Equal("bob@example.com", cmd.Parameters["p0"]);
        Assert.Equal(true, cmd.Parameters["p1"]);
    }

    [Fact]
    public void SetExpr_emits_raw_expression_without_a_parameter()
    {
        var cmd = SurrealQuery.Create("user")
            .Set("email", "a")
            .SetExpr("created_at", "time::now()")
            .Build();

        Assert.Equal("CREATE user SET email = $p0, created_at = time::now()", cmd.Sql);
        Assert.Single(cmd.Parameters);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetIfPresent_skips_null_or_empty_string(string? value)
    {
        var cmd = SurrealQuery.Create("user")
            .Set("email", "a")
            .SetIfPresent("nickname", value)
            .Build();

        Assert.Equal("CREATE user SET email = $p0", cmd.Sql);
    }

    [Fact]
    public void SetIfPresent_writes_when_value_present()
    {
        var cmd = SurrealQuery.Create("user")
            .SetIfPresent("nickname", "ali")
            .Build();

        Assert.Equal("CREATE user SET nickname = $p0", cmd.Sql);
        Assert.Equal("ali", cmd.Parameters["p0"]);
    }

    [Fact]
    public void Bind_registers_a_named_parameter_without_emitting_SET()
    {
        var cmd = SurrealQuery.Create("user")
            .SetExpr("id", "type::record('user', $custom)")
            .Bind("custom", "abc")
            .Build();

        Assert.Equal("CREATE user SET id = type::record('user', $custom)", cmd.Sql);
        Assert.Equal("abc", cmd.Parameters["custom"]);
    }

    [Theory]
    [InlineData("ReturnBefore", "BEFORE")]
    [InlineData("ReturnAfter",  "AFTER")]
    [InlineData("ReturnNone",   "NONE")]
    [InlineData("ReturnDiff",   "DIFF")]
    public void Return_modifiers_append_clause(string method, string expectedClause)
    {
        var b = SurrealQuery.Create("user").Set("email", "x");
        typeof(SurrealCreateBuilder).GetMethod(method)!.Invoke(b, null);
        var cmd = b.Build();

        Assert.EndsWith($"RETURN {expectedClause}", cmd.Sql);
    }

    [Fact]
    public void Return_custom_expression_is_emitted_verbatim()
    {
        var cmd = SurrealQuery.Create("user").Set("email", "x").Return("id, email").Build();
        Assert.EndsWith("RETURN id, email", cmd.Sql);
    }

    [Fact]
    public void Build_without_any_Set_clauses_throws_InvalidOperationException()
    {
        var b = SurrealQuery.Create("user");
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }
}
