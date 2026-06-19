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
}
