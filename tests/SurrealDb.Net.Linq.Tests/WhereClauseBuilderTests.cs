using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class WhereClauseBuilderTests
{
    [Fact]
    public void Empty_builder_has_no_clause()
    {
        var bag = new ParameterBag();
        var b = new WhereClauseBuilder(bag);

        Assert.False(b.HasClause);
        Assert.Equal(string.Empty, b.Render());
    }

    [Fact]
    public void First_Add_skips_conjunction_then_chains_with_AND()
    {
        var bag = new ParameterBag();
        var b = new WhereClauseBuilder(bag);

        b.Add("email", SurrealOperator.Equals, "alice", conjunction: "AND");
        b.Add("active", SurrealOperator.Equals, true, conjunction: "AND");

        Assert.True(b.HasClause);
        Assert.Equal("email = $p0 AND active = $p1", b.Render());
    }

    [Fact]
    public void Mixed_AND_OR_renders_with_each_specified_conjunction()
    {
        var bag = new ParameterBag();
        var b = new WhereClauseBuilder(bag);

        b.Add("a", SurrealOperator.Equals, 1, "AND");
        b.Add("b", SurrealOperator.Equals, 2, "OR");
        b.Add("c", SurrealOperator.Equals, 3, "AND");

        Assert.Equal("a = $p0 OR b = $p1 AND c = $p2", b.Render());
    }

    [Fact]
    public void Unary_operator_does_not_consume_a_parameter()
    {
        var bag = new ParameterBag();
        var b = new WhereClauseBuilder(bag);

        b.Add("deleted_at", SurrealOperator.IsNone, value: null, "AND");

        Assert.Equal("deleted_at IS NONE", b.Render());
        Assert.Empty(bag.Snapshot());
    }

    [Fact]
    public void AddRaw_wraps_in_parens_and_chains_with_conjunction()
    {
        var bag = new ParameterBag();
        var b = new WhereClauseBuilder(bag);

        b.Add("a", SurrealOperator.Equals, 1, "AND");
        b.AddRaw("x > 0 AND y < 10", "AND");

        Assert.Equal("a = $p0 AND (x > 0 AND y < 10)", b.Render());
    }

    [Fact]
    public void AddRaw_as_first_clause_omits_conjunction()
    {
        var bag = new ParameterBag();
        var b = new WhereClauseBuilder(bag);

        b.AddRaw("x = 1", "AND");

        Assert.Equal("(x = 1)", b.Render());
    }
}
