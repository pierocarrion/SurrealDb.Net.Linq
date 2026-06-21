using System.Linq.Expressions;
using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class ExpressionToWhereTests
{
    private static (string sql, IReadOnlyDictionary<string, object?> args) Translate<T>(Expression<Func<T, bool>> predicate)
    {
        var bag = new ParameterBag();
        var sql = ExpressionToWhere.Translate(predicate, bag);
        return (sql, bag.Snapshot());
    }

    // ---------- comparison operators ----------

    [Fact]
    public void Equals_against_constant_emits_field_eq_placeholder()
    {
        var (sql, args) = Translate<User>(u => u.Email == "alice@example.com");
        Assert.Equal("email = $p0", sql);
        Assert.Equal("alice@example.com", args["p0"]);
    }

    [Fact]
    public void NotEquals_emits_bang_eq()
    {
        var (sql, args) = Translate<User>(u => u.Salary != 0);
        Assert.Equal("salary != $p0", sql);
        Assert.Equal(0, args["p0"]);
    }

    [Theory]
    [InlineData("Salary", ExpressionType.LessThan,           "<")]
    [InlineData("Salary", ExpressionType.LessThanOrEqual,    "<=")]
    [InlineData("Salary", ExpressionType.GreaterThan,        ">")]
    [InlineData("Salary", ExpressionType.GreaterThanOrEqual, ">=")]
    public void Inequality_operators_render_correctly(string _, ExpressionType nodeType, string opString)
    {
        var p = Expression.Parameter(typeof(User), "u");
        var member = Expression.Property(p, nameof(User.Salary));
        var body = Expression.MakeBinary(nodeType, member, Expression.Constant(50_000));
        var lambda = Expression.Lambda<Func<User, bool>>(body, p);

        var bag = new ParameterBag();
        var sql = ExpressionToWhere.Translate(lambda, bag);

        Assert.Equal($"salary {opString} $p0", sql);
        Assert.Equal(50_000, bag.Snapshot()["p0"]);
    }

    [Fact]
    public void Field_on_right_side_swaps_and_flips_operator()
    {
        var (sql, args) = Translate<User>(u => 50_000 < u.Salary);
        // 50000 < salary  →  salary > 50000
        Assert.Equal("salary > $p0", sql);
        Assert.Equal(50_000, args["p0"]);
    }

    // ---------- logical operators ----------

    [Fact]
    public void AndAlso_combines_with_AND_and_wraps_in_parens()
    {
        var (sql, args) = Translate<User>(u => u.Email == "alice" && u.Active);
        Assert.Equal("(email = $p0 AND active = $p1)", sql);
        Assert.Equal("alice", args["p0"]);
        Assert.Equal(true, args["p1"]);
    }

    [Fact]
    public void OrElse_combines_with_OR()
    {
        var (sql, _) = Translate<User>(u => u.Salary > 100 || u.Salary < 10);
        Assert.Equal("(salary > $p0 OR salary < $p1)", sql);
    }

    [Fact]
    public void Not_wraps_inner_with_NOT()
    {
        var (sql, args) = Translate<User>(u => !u.Active);
        Assert.Equal("NOT (active = $p0)", sql);
        Assert.Equal(true, args["p0"]);
    }

    [Fact]
    public void Nested_AND_OR_keeps_grouping()
    {
        var (sql, _) = Translate<User>(u => (u.Active && u.Salary > 0) || u.Email == "x");
        Assert.Equal("((active = $p0 AND salary > $p1) OR email = $p2)", sql);
    }

    // ---------- bool member shorthand ----------

    [Fact]
    public void Bare_boolean_member_compiles_to_field_eq_true()
    {
        var (sql, args) = Translate<User>(u => u.Active);
        Assert.Equal("active = $p0", sql);
        Assert.Equal(true, args["p0"]);
    }

    // ---------- null comparisons ----------

    [Fact]
    public void Equal_to_null_emits_IS_NONE_with_no_parameter()
    {
        var (sql, args) = Translate<User>(u => u.Email == null);
        Assert.Equal("email IS NONE", sql);
        Assert.Empty(args);
    }

    [Fact]
    public void NotEqual_to_null_emits_IS_NOT_NONE_with_no_parameter()
    {
        var (sql, args) = Translate<User>(u => u.Email != null);
        Assert.Equal("email IS NOT NONE", sql);
        Assert.Empty(args);
    }

    // ---------- closure capture ----------

    [Fact]
    public void Captured_local_variable_is_evaluated_into_parameter()
    {
        var threshold = 42;
        var (sql, args) = Translate<User>(u => u.Salary > threshold);
        Assert.Equal("salary > $p0", sql);
        Assert.Equal(42, args["p0"]);
    }

    [Fact]
    public void Captured_method_call_is_evaluated_into_parameter()
    {
        var (sql, args) = Translate<User>(u => u.Email == GetEmail());
        Assert.Equal("email = $p0", sql);
        Assert.Equal("captured@example.com", args["p0"]);
    }

    private static string GetEmail() => "captured@example.com";

    // ---------- nested member access ----------

    [Fact]
    public void Nested_member_chain_renders_with_dots()
    {
        var (sql, args) = Translate<User>(u => u.Address!.City == "Lima");
        Assert.Equal("address.city = $p0", sql);
        Assert.Equal("Lima", args["p0"]);
    }

    // ---------- JsonPropertyName override ----------

    [Fact]
    public void JsonPropertyName_attribute_overrides_snake_case()
    {
        var when = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var (sql, args) = Translate<User>(u => u.HireDate >= when);
        Assert.Equal("hire_date_custom >= $p0", sql);
        Assert.Equal(when, args["p0"]);
    }

    // ---------- string methods ----------

    [Fact]
    public void StringContains_emits_string_contains_function()
    {
        var (sql, args) = Translate<User>(u => u.Email!.Contains("@example"));
        Assert.Equal("string::contains(email, $p0)", sql);
        Assert.Equal("@example", args["p0"]);
    }

    [Fact]
    public void StringStartsWith_emits_string_starts_with_function()
    {
        var (sql, args) = Translate<User>(u => u.Email!.StartsWith("alice"));
        Assert.Equal("string::starts_with(email, $p0)", sql);
        Assert.Equal("alice", args["p0"]);
    }

    [Fact]
    public void StringEndsWith_emits_string_ends_with_function()
    {
        var (sql, args) = Translate<User>(u => u.Email!.EndsWith(".com"));
        Assert.Equal("string::ends_with(email, $p0)", sql);
        Assert.Equal(".com", args["p0"]);
    }

    [Fact]
    public void IsNullOrEmpty_expands_to_IS_NONE_or_empty_string()
    {
        var (sql, args) = Translate<User>(u => string.IsNullOrEmpty(u.Email));
        Assert.Equal("(email IS NONE OR email = $p0)", sql);
        Assert.Equal(string.Empty, args["p0"]);
    }

    // ---------- collection containment ----------

    [Fact]
    public void Local_array_Contains_field_renders_as_field_IN_placeholder()
    {
        var allowed = new[] { "engineering", "design" };
        var (sql, args) = Translate<User>(u => allowed.Contains(u.Department!));
        Assert.Equal("department IN $p0", sql);
        Assert.Same(allowed, args["p0"]);
    }

    [Fact]
    public void Local_list_Contains_field_renders_as_field_IN_placeholder()
    {
        var allowed = new List<string> { "a", "b" };
        var (sql, _) = Translate<User>(u => allowed.Contains(u.Status!));
        Assert.Equal("status IN $p0", sql);
    }

    [Fact]
    public void Member_collection_Contains_constant_renders_as_field_CONTAINS_placeholder()
    {
        var (sql, args) = Translate<User>(u => u.Tags.Contains("admin"));
        Assert.Equal("tags CONTAINS $p0", sql);
        Assert.Equal("admin", args["p0"]);
    }

    // ---------- composite scenarios ----------

    [Fact]
    public void Real_world_filter_compiles_to_full_parametrized_query()
    {
        var department = "engineering";
        var minSalary = 50_000;

        var (sql, args) = Translate<User>(u =>
            u.Department == department
            && u.Salary >= minSalary
            && u.Active
            && u.Email != null);

        Assert.Equal(
            "(((department = $p0 AND salary >= $p1) AND active = $p2) AND email IS NOT NONE)",
            sql);
        Assert.Equal("engineering", args["p0"]);
        Assert.Equal(50_000, args["p1"]);
        Assert.Equal(true, args["p2"]);
        Assert.Equal(3, args.Count);
    }

    // ---------- unsupported shapes ----------

    [Fact]
    public void Unsupported_method_call_throws_NotSupportedException_with_actionable_message()
    {
        // string.PadLeft no está traducido — usa un método realmente no soportado.
        // (ToUpper/ToLower/Trim/Replace pasaron a estar soportados en 0.6.0.)
        var ex = Assert.Throws<NotSupportedException>(() =>
            Translate<User>(u => u.Email!.PadLeft(10) == "X"));

        Assert.Contains("WhereRaw", ex.Message);
    }

    [Fact]
    public void Two_field_comparison_renders_without_parameters()
    {
        // u.Status == u.Department  →  status = department  (no params)
        var (sql, args) = Translate<User>(u => u.Status == u.Department);
        Assert.Equal("status = department", sql);
        Assert.Empty(args);
    }
}
