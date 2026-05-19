using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealSelectBuilderTests
{
    [Fact]
    public void Minimal_From_emits_select_star_from_target()
    {
        var cmd = SurrealQuery.From("user").Build();

        Assert.Equal("SELECT * FROM user", cmd.Sql);
        Assert.Empty(cmd.Parameters);
    }

    [Fact]
    public void Select_replaces_projection_explicit_fields()
    {
        var cmd = SurrealQuery.From("user").Select("id", "email").Build();

        Assert.Equal("SELECT id, email FROM user", cmd.Sql);
    }

    [Fact]
    public void Field_appends_to_existing_projection()
    {
        var cmd = SurrealQuery.From("user").Select("id").Field("email").Build();

        Assert.Equal("SELECT id, email FROM user", cmd.Sql);
    }

    [Fact]
    public void SelectValue_emits_SELECT_VALUE_expression()
    {
        var cmd = SurrealQuery.From("user").SelectValue("<string>id").Build();

        Assert.Equal("SELECT VALUE <string>id FROM user", cmd.Sql);
    }

    [Fact]
    public void Only_emits_FROM_ONLY()
    {
        var cmd = SurrealQuery.From("user:abc").Only().Build();

        Assert.Equal("SELECT * FROM ONLY user:abc", cmd.Sql);
    }

    [Fact]
    public void Live_prefixes_with_LIVE()
    {
        var cmd = SurrealQuery.Live("ticket").Build();

        Assert.Equal("LIVE SELECT * FROM ticket", cmd.Sql);
    }

    [Fact]
    public void Where_chain_uses_AND_by_default_And_alias_works()
    {
        var cmd = SurrealQuery.From("user")
            .Where("email", SurrealOperator.Equals, "alice@example.com")
            .And("active", SurrealOperator.Equals, true)
            .Build();

        Assert.Equal("SELECT * FROM user WHERE email = $p0 AND active = $p1", cmd.Sql);
        Assert.Equal("alice@example.com", cmd.Parameters["p0"]);
        Assert.Equal(true, cmd.Parameters["p1"]);
    }

    [Fact]
    public void Or_emits_OR_conjunction()
    {
        var cmd = SurrealQuery.From("user")
            .Where("email", SurrealOperator.Equals, "a")
            .Or("email", SurrealOperator.Equals, "b")
            .Build();

        Assert.Equal("SELECT * FROM user WHERE email = $p0 OR email = $p1", cmd.Sql);
    }

    [Fact]
    public void OrderBy_with_default_direction_is_ASC()
    {
        var cmd = SurrealQuery.From("user").OrderBy("created_at").Build();

        Assert.Equal("SELECT * FROM user ORDER BY created_at ASC", cmd.Sql);
    }

    [Fact]
    public void OrderBy_explicit_Desc_emits_DESC()
    {
        var cmd = SurrealQuery.From("user").OrderBy("created_at", SortDirection.Desc).Build();

        Assert.Equal("SELECT * FROM user ORDER BY created_at DESC", cmd.Sql);
    }

    [Fact]
    public void Multiple_OrderBy_calls_emit_comma_separated_list()
    {
        var cmd = SurrealQuery.From("user")
            .OrderBy("dept")
            .OrderBy("salary", SortDirection.Desc)
            .Build();

        Assert.Equal("SELECT * FROM user ORDER BY dept ASC, salary DESC", cmd.Sql);
    }

    [Fact]
    public void GroupBy_emits_GROUP_BY_after_WHERE_and_before_ORDER_BY()
    {
        var cmd = SurrealQuery.From("user")
            .Where("active", SurrealOperator.Equals, true)
            .GroupBy("dept", "level")
            .OrderBy("dept")
            .Build();

        Assert.Equal(
            "SELECT * FROM user WHERE active = $p0 GROUP BY dept, level ORDER BY dept ASC",
            cmd.Sql);
    }

    [Fact]
    public void Limit_and_Start_emit_at_end()
    {
        var cmd = SurrealQuery.From("user").Limit(10).Start(20).Build();

        Assert.Equal("SELECT * FROM user LIMIT 10 START 20", cmd.Sql);
    }

    [Fact]
    public void Fetch_emits_FETCH_clause_last()
    {
        var cmd = SurrealQuery.From("article").Fetch("author", "tags").Build();

        Assert.Equal("SELECT * FROM article FETCH author, tags", cmd.Sql);
    }

    [Fact]
    public void WhereRaw_wraps_expression_in_parens_and_registers_named_params()
    {
        var cmd = SurrealQuery.From("user")
            .WhereRaw("manager.dept = $dept", new Dictionary<string, object?> { ["dept"] = "eng" })
            .Build();

        Assert.Equal("SELECT * FROM user WHERE (manager.dept = $dept)", cmd.Sql);
        Assert.Equal("eng", cmd.Parameters["dept"]);
    }

    [Fact]
    public void IS_NONE_operator_does_not_consume_a_parameter()
    {
        var cmd = SurrealQuery.From("user")
            .Where("deleted_at", SurrealOperator.IsNone)
            .Build();

        Assert.Equal("SELECT * FROM user WHERE deleted_at IS NONE", cmd.Sql);
        Assert.Empty(cmd.Parameters);
    }
}
