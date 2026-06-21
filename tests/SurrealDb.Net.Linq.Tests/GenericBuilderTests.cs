using System.Linq.Expressions;
using Xunit;

namespace SurrealDb.Net.Linq.Tests;

/// <summary>
/// Tests for the generic Create&lt;T&gt;/Update&lt;T&gt;/Upsert&lt;T&gt;
/// builders added in 0.5.0. Member resolution uses [JsonPropertyName] or
/// snake_case fallback (same path as Where lambdas).
/// </summary>
public class GenericBuilderTests
{
    private sealed class User
    {
        public string Email { get; set; } = "";
        public int Age { get; set; }
        public bool Active { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
        public Address Home { get; set; } = new();
    }

    private sealed class Address
    {
        public string City { get; set; } = "";
    }

    // ── Create<T> ───────────────────────────────────────────────────────

    [Fact]
    public void Create_T_Set_lambda_uses_snake_case_field_name()
    {
        var cmd = SurrealQuery.Create<User>("user")
            .Set(u => u.Email, "alice@x.com")
            .Set(u => u.Age, 30)
            .Build();

        Assert.Contains("email = $p0", cmd.Sql);
        Assert.Contains("age = $p1", cmd.Sql);
        Assert.Equal("alice@x.com", cmd.Parameters["p0"]);
        Assert.Equal(30, cmd.Parameters["p1"]);
    }

    [Fact]
    public void Create_T_Set_lambda_respects_JsonPropertyName()
    {
        var now = DateTimeOffset.UtcNow;
        var cmd = SurrealQuery.Create<User>("user")
            .Set(u => u.CreatedAt, now)
            .Build();

        Assert.Contains("created_at = $p0", cmd.Sql);
        Assert.Equal(now, cmd.Parameters["p0"]);
    }

    [Fact]
    public void Create_T_Set_lambda_handles_nested_chains()
    {
        var cmd = SurrealQuery.Create<User>("user")
            .Set(u => u.Home.City, "Lima")
            .Build();

        Assert.Contains("home.city = $p0", cmd.Sql);
    }

    [Fact]
    public void Create_T_SetIfPresent_skips_when_null()
    {
        var cmd = SurrealQuery.Create<User>("user")
            .Set(u => u.Email, "x")
            .SetIfPresent(u => u.Age, null)
            .Build();

        Assert.DoesNotContain("age", cmd.Sql);
    }

    [Fact]
    public void Create_T_Return_enum_emits_rendered_value()
    {
        var cmd = SurrealQuery.Create<User>("user")
            .Set(u => u.Email, "x")
            .Return(SurrealReturn.After)
            .Build();

        Assert.Contains("RETURN AFTER", cmd.Sql);
    }

    [Fact]
    public void Create_T_ReturnFields_emits_comma_separated()
    {
        var cmd = SurrealQuery.Create<User>("user")
            .Set(u => u.Email, "x")
            .ReturnFields("id", "email")
            .Build();

        Assert.Contains("RETURN id, email", cmd.Sql);
    }

    // ── Update<T> ───────────────────────────────────────────────────────

    [Fact]
    public void Update_T_Where_lambda_and_Set_lambda_combine()
    {
        var cmd = SurrealQuery.Update<User>("user")
            .Where(u => u.Email == "alice@x.com")
            .Set(u => u.Age, 31)
            .Build();

        // Where clauses are wrapped in parens by WhereClauseBuilder for safe composition.
        Assert.Contains("UPDATE user SET age = $p1 WHERE (email = $p0)", cmd.Sql);
        Assert.Equal("alice@x.com", cmd.Parameters["p0"]);
        Assert.Equal(31, cmd.Parameters["p1"]);
    }

    [Fact]
    public void Update_T_Upsert_emits_UPSERT_keyword()
    {
        var cmd = SurrealQuery.Upsert<User>("user")
            .Set(u => u.Email, "x")
            .Build();

        Assert.StartsWith("UPSERT user", cmd.Sql);
    }

    // ── SurrealReturn enum (non-generic paths) ──────────────────────────

    [Fact]
    public void Update_Return_enum_emits_NONE()
    {
        var cmd = SurrealQuery.Update("user")
            .Set("a", 1)
            .Return(SurrealReturn.None)
            .Build();

        Assert.Contains("RETURN NONE", cmd.Sql);
    }

    [Fact]
    public void Delete_Return_enum_emits_DIFF()
    {
        var cmd = SurrealQuery.Delete("user")
            .Return(SurrealReturn.Diff)
            .Build();

        Assert.Contains("RETURN DIFF", cmd.Sql);
    }

    [Fact]
    public void Delete_ReturnAfter_emits_AFTER()
    {
        var cmd = SurrealQuery.Delete("user").ReturnAfter().Build();
        Assert.Contains("RETURN AFTER", cmd.Sql);
    }

    // ── Delete parity (Only, Limit, Start) ──────────────────────────────

    [Fact]
    public void Delete_Only_emits_DELETE_ONLY()
    {
        var cmd = SurrealQuery.Delete("user:abc").Only().Build();
        Assert.Contains("DELETE ONLY user:abc", cmd.Sql);
    }

    [Fact]
    public void Delete_Limit_emits_LIMIT()
    {
        var cmd = SurrealQuery.Delete("session")
            .Where("expired", SurrealOperator.Equals, true)
            .Limit(10)
            .Build();
        Assert.Contains("LIMIT 10", cmd.Sql);
    }

    [Fact]
    public void Delete_Limit_Start_emits_both()
    {
        var cmd = SurrealQuery.Delete("session")
            .Where("expired", SurrealOperator.Equals, true)
            .Limit(10)
            .Start(5)
            .Build();
        Assert.Contains("LIMIT 10 START 5", cmd.Sql);
    }
}
