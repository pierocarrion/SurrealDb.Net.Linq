using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealOperatorRendererTests
{
    [Theory]
    [InlineData(SurrealOperator.Equals,         "name = $p0")]
    [InlineData(SurrealOperator.NotEquals,      "name != $p0")]
    [InlineData(SurrealOperator.Greater,        "name > $p0")]
    [InlineData(SurrealOperator.GreaterOrEqual, "name >= $p0")]
    [InlineData(SurrealOperator.Less,           "name < $p0")]
    [InlineData(SurrealOperator.LessOrEqual,    "name <= $p0")]
    [InlineData(SurrealOperator.In,             "name IN $p0")]
    [InlineData(SurrealOperator.NotIn,          "name NOT IN $p0")]
    [InlineData(SurrealOperator.Contains,       "name CONTAINS $p0")]
    [InlineData(SurrealOperator.ContainsNot,    "name CONTAINSNOT $p0")]
    [InlineData(SurrealOperator.Inside,         "name INSIDE $p0")]
    [InlineData(SurrealOperator.Like,           "string::contains(name, $p0)")]
    public void Render_binary_operators_emit_field_op_placeholder(SurrealOperator op, string expected)
    {
        Assert.Equal(expected, SurrealOperatorRenderer.Render("name", op, "$p0"));
    }

    [Theory]
    [InlineData(SurrealOperator.IsNone,    "x IS NONE")]
    [InlineData(SurrealOperator.IsNotNone, "x IS NOT NONE")]
    public void Render_unary_operators_ignore_placeholder(SurrealOperator op, string expected)
    {
        Assert.Equal(expected, SurrealOperatorRenderer.Render("x", op, null));
    }

    [Theory]
    [InlineData(SurrealOperator.Equals,    true)]
    [InlineData(SurrealOperator.Contains,  true)]
    [InlineData(SurrealOperator.Like,      true)]
    [InlineData(SurrealOperator.IsNone,    false)]
    [InlineData(SurrealOperator.IsNotNone, false)]
    public void IsBinary_returns_false_only_for_null_check_operators(SurrealOperator op, bool isBinary)
    {
        Assert.Equal(isBinary, SurrealOperatorRenderer.IsBinary(op));
    }
}
