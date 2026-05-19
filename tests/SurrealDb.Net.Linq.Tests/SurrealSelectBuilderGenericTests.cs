using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealSelectBuilderGenericTests
{
    [Fact]
    public void From_T_lambda_Where_emits_typed_predicate_as_parens_wrapped_clause()
    {
        var cmd = SurrealQuery.From<User>("user")
            .Where(u => u.Email == "alice@example.com")
            .Build();

        Assert.Equal("SELECT * FROM user WHERE (email = $p0)", cmd.Sql);
        Assert.Equal("alice@example.com", cmd.Parameters["p0"]);
    }

    [Fact]
    public void Multi_predicate_chain_combines_with_AND()
    {
        var cmd = SurrealQuery.From<User>("user")
            .Where(u => u.Active)
            .And(u => u.Salary > 50_000)
            .Build();

        Assert.Equal("SELECT * FROM user WHERE (active = $p0) AND (salary > $p1)", cmd.Sql);
        Assert.Equal(true, cmd.Parameters["p0"]);
        Assert.Equal(50_000, cmd.Parameters["p1"]);
    }

    [Fact]
    public void Or_lambda_chains_with_OR()
    {
        var cmd = SurrealQuery.From<User>("user")
            .Where(u => u.Email == "a")
            .Or(u => u.Email == "b")
            .Build();

        Assert.Equal("SELECT * FROM user WHERE (email = $p0) OR (email = $p1)", cmd.Sql);
    }

    [Fact]
    public void Lambda_and_string_predicates_can_be_mixed_in_one_chain()
    {
        var cmd = SurrealQuery.From<User>("user")
            .Where(u => u.Active)
            .And("email", SurrealOperator.Equals, "x")
            .Build();

        Assert.Equal("SELECT * FROM user WHERE (active = $p0) AND email = $p1", cmd.Sql);
        Assert.Equal(true, cmd.Parameters["p0"]);
        Assert.Equal("x", cmd.Parameters["p1"]);
    }

    [Fact]
    public void Full_typed_chain_with_projection_orderby_limit_and_fetch_builds_complete_query()
    {
        var cmd = SurrealQuery.From<User>("user")
            .Select("id", "email", "department")
            .Where(u => u.Department == "engineering" && u.Salary >= 50_000)
            .OrderBy("hire_date_custom", SortDirection.Desc)
            .Limit(50)
            .Fetch("manager")
            .Build();

        Assert.Equal(
            "SELECT id, email, department FROM user " +
            "WHERE ((department = $p0 AND salary >= $p1)) " +
            "ORDER BY hire_date_custom DESC " +
            "LIMIT 50 " +
            "FETCH manager",
            cmd.Sql);
        Assert.Equal("engineering", cmd.Parameters["p0"]);
        Assert.Equal(50_000, cmd.Parameters["p1"]);
    }

    [Fact]
    public void Live_T_lambda_emits_LIVE_SELECT_with_typed_predicate()
    {
        var cmd = SurrealQuery.Live<User>("user")
            .Where(u => u.Active)
            .Build();

        Assert.Equal("LIVE SELECT * FROM user WHERE (active = $p0)", cmd.Sql);
    }

    [Fact]
    public void JsonPropertyName_attribute_is_honored_in_lambda_field_resolution()
    {
        var when = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var cmd = SurrealQuery.From<User>("user")
            .Where(u => u.HireDate >= when)
            .Build();

        Assert.Equal("SELECT * FROM user WHERE (hire_date_custom >= $p0)", cmd.Sql);
    }

    [Fact]
    public void Build_can_be_called_repeatedly_without_side_effects()
    {
        var b = SurrealQuery.From<User>("user").Where(u => u.Active);

        var first = b.Build();
        var second = b.Build();

        Assert.Equal(first.Sql, second.Sql);
        Assert.Equal(first.Parameters, second.Parameters);
    }
}
