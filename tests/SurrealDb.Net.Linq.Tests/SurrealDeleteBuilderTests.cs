using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealDeleteBuilderTests
{
    [Fact]
    public void Minimal_delete_emits_DELETE_target()
    {
        var cmd = SurrealQuery.Delete("session").Build();
        Assert.Equal("DELETE session", cmd.Sql);
        Assert.Empty(cmd.Parameters);
    }

    [Fact]
    public void Delete_with_where_emits_DELETE_target_WHERE_clause()
    {
        var when = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var cmd = SurrealQuery.Delete("session")
            .Where("expires_at", SurrealOperator.Less, when)
            .Build();

        Assert.Equal("DELETE session WHERE expires_at < $p0", cmd.Sql);
        Assert.Equal(when, cmd.Parameters["p0"]);
    }

    [Theory]
    [InlineData("ReturnBefore", "BEFORE")]
    [InlineData("ReturnDiff",   "DIFF")]
    [InlineData("ReturnNone",   "NONE")]
    public void Return_modifiers_emit_correct_clause(string method, string expectedClause)
    {
        var b = SurrealQuery.Delete("x");
        typeof(SurrealDeleteBuilder).GetMethod(method)!.Invoke(b, null);
        var cmd = b.Build();

        Assert.Equal($"DELETE x RETURN {expectedClause}", cmd.Sql);
    }

    [Fact]
    public void Delete_T_lambda_emits_typed_predicate_wrapped_in_parens()
    {
        var cmd = SurrealQuery.Delete<User>("user")
            .Where(u => !u.Active)
            .Build();

        Assert.Equal("DELETE user WHERE (NOT (active = $p0))", cmd.Sql);
        Assert.Equal(true, cmd.Parameters["p0"]);
    }

    [Fact]
    public void Delete_T_and_or_chain()
    {
        var cmd = SurrealQuery.Delete<User>("user")
            .Where(u => u.Email == "a")
            .Or(u => u.Email == "b")
            .Build();

        Assert.Equal("DELETE user WHERE (email = $p0) OR (email = $p1)", cmd.Sql);
    }
}
