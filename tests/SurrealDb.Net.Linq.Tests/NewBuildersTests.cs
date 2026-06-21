using Xunit;

namespace SurrealDb.Net.Linq.Tests;

/// <summary>
/// Tests para SurrealQuery.Insert / Relate / SetOperation y clauses nuevas
/// de 0.6.0 (CONTENT/MERGE/PATCH, SPLIT, EXPLAIN, PARALLEL, TIMEOUT, VERSION).
/// </summary>
public class NewBuildersTests
{
    // ── INSERT ──────────────────────────────────────────────────────────

    [Fact]
    public void Insert_single_row_emits_INSERT_INTO_VALUES()
    {
        var cmd = SurrealQuery.Insert("user")
            .Columns("email", "active")
            .Values("alice@x.com", true)
            .Build();

        Assert.Equal("INSERT INTO user (email, active) VALUES ($p0, $p1)", cmd.Sql);
        Assert.Equal("alice@x.com", cmd.Parameters["p0"]);
        Assert.Equal(true, cmd.Parameters["p1"]);
    }

    [Fact]
    public void Insert_multiple_rows_emits_comma_separated_VALUES()
    {
        var cmd = SurrealQuery.Insert("user")
            .Columns("email")
            .Values("a@x.com")
            .Values("b@x.com")
            .Build();

        Assert.Equal("INSERT INTO user (email) VALUES ($p0), ($p1)", cmd.Sql);
    }

    [Fact]
    public void Insert_without_columns_omits_column_list()
    {
        var cmd = SurrealQuery.Insert("user")
            .Values("a@x.com", true)
            .Build();

        Assert.Equal("INSERT INTO user VALUES ($p0, $p1)", cmd.Sql);
    }

    [Fact]
    public void Insert_values_count_mismatch_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SurrealQuery.Insert("user")
                .Columns("a", "b")
                .Values("only-one")
                .Build());
    }

    [Fact]
    public void Insert_with_on_duplicate_key_update_emits_clause()
    {
        var cmd = SurrealQuery.Insert("user")
            .Columns("email")
            .Values("a@x.com")
            .OnDuplicateKeyUpdate("email", "email")
            .ReturnAfter()
            .Build();

        Assert.Contains("ON DUPLICATE KEY UPDATE email = email", cmd.Sql);
        Assert.Contains("RETURN AFTER", cmd.Sql);
    }

    [Fact]
    public void Insert_without_values_throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SurrealQuery.Insert("user").Columns("a").Build());
    }

    // ── RELATE ──────────────────────────────────────────────────────────

    [Fact]
    public void Relate_emits_RELATE_with_parameterized_source_and_target()
    {
        var cmd = SurrealQuery.Relate("user:abc", "wrote", "article:xyz")
            .Set("since", DateTimeOffset.UtcNow)
            .ReturnAfter()
            .Build();

        Assert.Contains("RELATE $p0 -> wrote -> $p1", cmd.Sql);
        Assert.Contains("SET since = $p2", cmd.Sql);
        Assert.Contains("RETURN AFTER", cmd.Sql);
    }

    [Fact]
    public void Relate_unique_emits_RELATE_UNIQUE()
    {
        var cmd = SurrealQuery.RelateUnique("u:1", "follows", "u:2").Build();

        Assert.Contains("RELATE UNIQUE", cmd.Sql);
    }

    [Fact]
    public void Relate_with_set_expr_emits_correct_clause()
    {
        var cmd = SurrealQuery.Relate("u:1", "rated", "u:2")
            .SetExpr("weight", "math::rand()")
            .Build();

        Assert.Contains("weight = math::rand()", cmd.Sql);
    }

    // ── Set operations ──────────────────────────────────────────────────

    [Fact]
    public void SetOperation_UNION_combines_two_selects()
    {
        var left = SurrealQuery.From("user").Select("id").Build();
        var right = SurrealQuery.From("admin").Select("id").Build();

        var cmd = SurrealQuery.SetOperation()
            .From(left)
            .Union(right)
            .Build();

        Assert.Contains("UNION", cmd.Sql);
        Assert.StartsWith("(SELECT id FROM user)", cmd.Sql);
        Assert.Contains("(SELECT id FROM admin)", cmd.Sql);
    }

    [Fact]
    public void SetOperation_INTERSECT_rebases_parameters()
    {
        var left = SurrealQuery.From("user").Where("active", SurrealOperator.Equals, true).Build();
        var right = SurrealQuery.From("user").Where("age", SurrealOperator.Greater, 18).Build();

        var cmd = SurrealQuery.SetOperation()
            .From(left)
            .Intersect(right)
            .Build();

        Assert.Contains("$o0_p0", cmd.Sql);
        Assert.Contains("$o1_p0", cmd.Sql);
        Assert.Equal(true, cmd.Parameters["o0_p0"]);
        Assert.Equal(18, cmd.Parameters["o1_p0"]);
    }

    [Fact]
    public void SetOperation_requires_two_operands()
    {
        var left = SurrealQuery.From("user").Build();
        Assert.Throws<InvalidOperationException>(() =>
            SurrealQuery.SetOperation().From(left).Build());
    }

    [Fact]
    public void SetOperation_UNION_ALL_DISTINCT_operators()
    {
        var a = SurrealQuery.From("a").Build();
        var b = SurrealQuery.From("b").Build();
        var c = SurrealQuery.From("c").Build();

        var cmd = SurrealQuery.SetOperation()
            .From(a)
            .UnionAll(b)
            .Difference(c)
            .Build();

        Assert.Contains("UNION ALL", cmd.Sql);
        Assert.Contains("DIFFERENCE", cmd.Sql);
    }

    // ── Nuevos operadores ───────────────────────────────────────────────

    [Fact]
    public void Matches_operator_emits_tilde()
    {
        var cmd = SurrealQuery.From("user")
            .Where("email", SurrealOperator.Matches, "@example\\.com$").Build();

        Assert.Contains("email ~ $p0", cmd.Sql);
    }

    [Fact]
    public void NotMatches_operator_emits_bang_tilde()
    {
        var cmd = SurrealQuery.From("user")
            .Where("email", SurrealOperator.NotMatches, "@spam").Build();

        Assert.Contains("email !~ $p0", cmd.Sql);
    }

    [Fact]
    public void Outside_operator_emits_OUTSIDE()
    {
        var cmd = SurrealQuery.From("user")
            .Where("location", SurrealOperator.Outside, new { x = 1, y = 2, r = 5 }).Build();
        Assert.Contains("location OUTSIDE $p0", cmd.Sql);
    }

    [Fact]
    public void Intersects_operator_emits_INTERSECTS()
    {
        var cmd = SurrealQuery.From("user")
            .Where("area", SurrealOperator.Intersects, new { x = 1, y = 2 }).Build();
        Assert.Contains("area INTERSECTS $p0", cmd.Sql);
    }

    // ── CONTENT / MERGE / PATCH ─────────────────────────────────────────

    [Fact]
    public void Create_Content_emits_CONTENT_clause()
    {
        var cmd = SurrealQuery.Create("user")
            .Content("$body")
            .Bind("body", new { email = "a@x.com", age = 30 })
            .Build();

        Assert.Contains("CREATE user CONTENT $body", cmd.Sql);
    }

    [Fact]
    public void Create_Merge_emits_MERGE_clause()
    {
        var cmd = SurrealQuery.Create("user")
            .Merge("$patch").Bind("patch", new { a = 1 }).Build();

        Assert.Contains("CREATE user MERGE $patch", cmd.Sql);
    }

    [Fact]
    public void Create_Patch_emits_PATCH_clause()
    {
        var cmd = SurrealQuery.Create("user")
            .Patch("$op").Bind("op", "[{op:'replace',path:'/a',value:1}]").Build();

        Assert.Contains("CREATE user PATCH $op", cmd.Sql);
    }

    [Fact]
    public void Create_Set_and_Content_are_mutually_exclusive()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SurrealQuery.Create("user").Set("a", 1).Content("$x").Build());
    }

    [Fact]
    public void Update_Merge_emits_MERGE_clause()
    {
        var cmd = SurrealQuery.Update("user:abc").Merge("$p").Bind("p", new { a = 1 }).Build();

        Assert.Contains("UPDATE user:abc MERGE $p", cmd.Sql);
    }

    // ── SPLIT / EXPLAIN / PARALLEL / TIMEOUT / VERSION ──────────────────

    [Fact]
    public void Select_SplitOn_emits_SPLIT_ON()
    {
        var cmd = SurrealQuery.From("order").SplitOn("customer_id", "product_id").Build();

        Assert.Contains("SPLIT ON customer_id, product_id", cmd.Sql);
    }

    [Fact]
    public void Select_Explain_emits_EXPLAIN()
    {
        var cmd = SurrealQuery.From("user").Explain().Build();
        Assert.Contains(" EXPLAIN", cmd.Sql);
    }

    [Fact]
    public void Select_ExplainFull_emits_EXPLAIN_FULL()
    {
        var cmd = SurrealQuery.From("user").Explain(full: true).Build();
        Assert.Contains("EXPLAIN FULL", cmd.Sql);
    }

    [Fact]
    public void Select_Parallel_emits_PARALLEL()
    {
        var cmd = SurrealQuery.From("user").Parallel().Build();
        Assert.EndsWith(" PARALLEL", cmd.Sql);
    }

    [Fact]
    public void Select_Timeout_emits_TIMEOUT()
    {
        var cmd = SurrealQuery.From("user").Timeout(TimeSpan.FromSeconds(5)).Build();
        Assert.Contains("TIMEOUT 5000ms", cmd.Sql);
    }

    [Fact]
    public void Select_Timeout_zero_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurrealQuery.From("user").Timeout(TimeSpan.Zero));
    }

    [Fact]
    public void Select_Version_emits_VERSION_with_iso8601()
    {
        var ts = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var cmd = SurrealQuery.From("user").Version(ts).Build();
        Assert.Contains("VERSION '2025-01-15T12:00:00.0000000+00:00'", cmd.Sql);
    }

    [Fact]
    public void Select_full_clause_chain_emits_in_canonical_order()
    {
        var ts = DateTimeOffset.UtcNow;
        var cmd = SurrealQuery.From("order")
            .Select("id", "total")
            .Where("active", SurrealOperator.Equals, true)
            .OrderBy("created_at", SortDirection.Desc)
            .Limit(10)
            .SplitOn("customer_id")
            .Fetch("customer")
            .Version(ts)
            .Explain(full: true)
            .Timeout(TimeSpan.FromSeconds(10))
            .Parallel()
            .Build();

        var sql = cmd.Sql;
        // Verificar el orden canónico SurrealQL: WHERE → ORDER → LIMIT → SPLIT → FETCH → VERSION → EXPLAIN → TIMEOUT → PARALLEL
        Assert.Contains("WHERE", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("SPLIT ON", sql);
        Assert.Contains("FETCH", sql);
        Assert.Contains("VERSION", sql);
        Assert.Contains("EXPLAIN FULL", sql);
        Assert.Contains("TIMEOUT", sql);
        Assert.EndsWith(" PARALLEL", sql);

        var whereIdx = sql.IndexOf("WHERE", StringComparison.Ordinal);
        var orderIdx = sql.IndexOf("ORDER BY", StringComparison.Ordinal);
        var limitIdx = sql.IndexOf("LIMIT", StringComparison.Ordinal);
        var splitIdx = sql.IndexOf("SPLIT ON", StringComparison.Ordinal);
        var fetchIdx = sql.IndexOf("FETCH", StringComparison.Ordinal);
        var versionIdx = sql.IndexOf("VERSION", StringComparison.Ordinal);
        var explainIdx = sql.IndexOf("EXPLAIN", StringComparison.Ordinal);
        var timeoutIdx = sql.IndexOf("TIMEOUT", StringComparison.Ordinal);
        var parallelIdx = sql.IndexOf("PARALLEL", StringComparison.Ordinal);

        Assert.True(whereIdx < orderIdx);
        Assert.True(orderIdx < limitIdx);
        Assert.True(limitIdx < splitIdx);
        Assert.True(splitIdx < fetchIdx);
        Assert.True(fetchIdx < versionIdx);
        Assert.True(versionIdx < explainIdx);
        Assert.True(explainIdx < timeoutIdx);
        Assert.True(timeoutIdx < parallelIdx);
    }
}
