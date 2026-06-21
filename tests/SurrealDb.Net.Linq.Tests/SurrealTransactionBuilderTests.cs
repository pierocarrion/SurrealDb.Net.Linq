using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealTransactionBuilderTests
{
    [Fact]
    public void Commit_transaction_wraps_statements_with_begin_and_commit()
    {
        var cmd = SurrealQuery.BeginTransaction()
            .Add(SurrealQuery.Create("audit").Set("action", "created").Build())
            .Add(SurrealQuery.Update("user").Set("active", true).Build())
            .Commit()
            .Build();

        var normalized = cmd.Sql.ReplaceLineEndings("\n");
        Assert.Equal(
            "BEGIN;\n" +
            "CREATE audit SET action = $s0_p0;\n" +
            "UPDATE user SET active = $s1_p0;\n" +
            "COMMIT;",
            normalized);
        Assert.Equal("created", cmd.Parameters["s0_p0"]);
        Assert.Equal(true, cmd.Parameters["s1_p0"]);
    }

    [Fact]
    public void Rollback_transaction_ends_with_cancel()
    {
        var cmd = SurrealQuery.BeginTransaction()
            .Add(SurrealQuery.Delete("session").Where("expired", SurrealOperator.Equals, true).Build())
            .Rollback()
            .Build();

        Assert.EndsWith("CANCEL;", cmd.Sql);
        Assert.StartsWith("BEGIN;", cmd.Sql);
    }

    [Fact]
    public void Parameters_with_colliding_names_are_rebased_per_statement()
    {
        var cmd = SurrealQuery.BeginTransaction()
            .Add(SurrealQuery.Create("user").Set("email", "a@x.com").Build())
            .Add(SurrealQuery.Create("user").Set("email", "b@x.com").Build())
            .Commit()
            .Build();

        Assert.Contains("$s0_p0", cmd.Sql);
        Assert.Contains("$s1_p0", cmd.Sql);
        Assert.Equal("a@x.com", cmd.Parameters["s0_p0"]);
        Assert.Equal("b@x.com", cmd.Parameters["s1_p0"]);
    }

    [Fact]
    public void Named_parameters_are_rebased_to_avoid_collisions()
    {
        var cmd = SurrealQuery.BeginTransaction()
            .Add(SurrealQuery.Raw("RETURN $live", new Dictionary<string, object?> { ["live"] = new Guid("11111111-1111-1111-1111-111111111111") }))
            .Add(SurrealQuery.Raw("RETURN $live", new Dictionary<string, object?> { ["live"] = new Guid("22222222-2222-2222-2222-222222222222") }))
            .Commit()
            .Build();

        Assert.Contains("$s0_live", cmd.Sql);
        Assert.Contains("$s1_live", cmd.Sql);
        Assert.Equal(new Guid("11111111-1111-1111-1111-111111111111"), cmd.Parameters["s0_live"]);
        Assert.Equal(new Guid("22222222-2222-2222-2222-222222222222"), cmd.Parameters["s1_live"]);
    }

    [Fact]
    public void Build_without_statements_throws_InvalidOperationException()
    {
        var tx = SurrealQuery.BeginTransaction();
        Assert.Throws<InvalidOperationException>(() => tx.Build());
    }

    [Fact]
    public void Build_without_Commit_or_Rollback_throws_InvalidOperationException()
    {
        var tx = SurrealQuery.BeginTransaction()
            .Add(SurrealQuery.Create("user").Set("email", "x").Build());
        Assert.Throws<InvalidOperationException>(() => tx.Build());
    }

    [Fact]
    public void Add_null_command_throws_ArgumentNullException()
    {
        var tx = SurrealQuery.BeginTransaction();
        Assert.Throws<ArgumentNullException>(() => tx.Add(null!));
    }

    // ────────────────────────────────────────────────────────────────────
    // Regresión T7 (0.4.0): el regex anterior (\$\w+) reescribía $word
    // dentro de literales string y corrompía el SQL.
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Preserves_dollar_signs_in_string_literals_inside_Raw_statements()
    {
        // Antes del fix, $5.00 dentro del literal se rebasaba a $s0_5.00.
        // Ahora el rebase sólo opera sobre placeholders conocidos.
        var raw = SurrealQuery.Raw(
            "SELECT * FROM product WHERE label CONTAINS 'price: $5.00'",
            new Dictionary<string, object?>());

        var tx = SurrealQuery.BeginTransaction()
            .Add(raw)
            .Commit()
            .Build();

        Assert.Contains("'price: $5.00'", tx.Sql);
        Assert.DoesNotContain("$s0_", tx.Sql);
    }

    [Fact]
    public void Preserves_dollar_signs_when_other_statement_has_real_placeholders()
    {
        // Mezcla: statement 0 con $word literal (sin parámetros), statement 1
        // con placeholders reales. El rebase debe operar sólo sobre los
        // placeholders del statement 1, no sobre el literal del 0.
        var raw = SurrealQuery.Raw(
            "SELECT * FROM t WHERE note CONTAINS 'cost: $99'",
            new Dictionary<string, object?>());
        var cmd = SurrealQuery.Create("audit").Set("action", "x").Build();

        var tx = SurrealQuery.BeginTransaction()
            .Add(raw)
            .Add(cmd)
            .Commit()
            .Build();

        Assert.Contains("'cost: $99'", tx.Sql);
        Assert.Contains("$s1_p0", tx.Sql);
        Assert.DoesNotContain("$s0_", tx.Sql);
    }

    [Fact]
    public void Rebases_only_known_placeholders_in_typed_builder_statements()
    {
        var cmd = SurrealQuery.From("user").Where("id", SurrealOperator.Equals, "x").Build();

        var tx = SurrealQuery.BeginTransaction()
            .Add(cmd)
            .Commit()
            .Build();

        Assert.Contains("$s0_p0", tx.Sql);
        Assert.DoesNotContain(" $p0 ", tx.Sql);
        Assert.DoesNotContain("= $p0", tx.Sql);
    }

    [Fact]
    public void Exposes_Placeholders_on_built_command()
    {
        var cmd = SurrealQuery.From("user").Where("a", SurrealOperator.Equals, 1).Where("b", SurrealOperator.Equals, 2).Build();

        Assert.Equal(new[] { "p0", "p1" }, cmd.Placeholders);
    }

    [Fact]
    public void Transaction_propagates_combined_placeholders()
    {
        var cmd0 = SurrealQuery.Create("t").Set("a", 1).Build();
        var cmd1 = SurrealQuery.Create("t").Set("b", 2).Build();

        var tx = SurrealQuery.BeginTransaction()
            .Add(cmd0)
            .Add(cmd1)
            .Commit()
            .Build();

        Assert.Equal(new[] { "s0_p0", "s1_p0" }, tx.Placeholders);
    }
}
