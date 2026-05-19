using SurrealDb.Net.Models;
using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealUpdateBuilderTests
{
    [Fact]
    public void Update_against_string_target_emits_UPDATE_target_SET()
    {
        var cmd = SurrealQuery.Update("user")
            .Set("active", false)
            .Build();

        Assert.Equal("UPDATE user SET active = $p0", cmd.Sql);
        Assert.Equal(false, cmd.Parameters["p0"]);
    }

    [Fact]
    public void Upsert_emits_UPSERT_keyword()
    {
        var cmd = SurrealQuery.Upsert("user")
            .Set("active", true)
            .Build();

        Assert.StartsWith("UPSERT user SET", cmd.Sql);
    }

    [Fact]
    public void UpdateRecord_binds_record_id_as_first_parameter_and_uses_placeholder_as_target()
    {
        var id = RecordId.From("user", "abc");

        var cmd = SurrealQuery.UpdateRecord(id)
            .Set("active", false)
            .Build();

        Assert.Equal("UPDATE $p0 SET active = $p1", cmd.Sql);
        Assert.Same(id, cmd.Parameters["p0"]);
        Assert.Equal(false, cmd.Parameters["p1"]);
    }

    [Fact]
    public void UpsertRecord_binds_record_id_and_uses_UPSERT_keyword()
    {
        var id = RecordId.From("user", "abc");
        var cmd = SurrealQuery.UpsertRecord(id).Set("active", true).Build();

        Assert.StartsWith("UPSERT $p0 SET", cmd.Sql);
        Assert.Same(id, cmd.Parameters["p0"]);
    }

    [Fact]
    public void Update_with_WHERE_appends_clause_after_SET()
    {
        var cmd = SurrealQuery.Update("user")
            .Set("active", false)
            .Where("email", SurrealOperator.Equals, "x")
            .Build();

        Assert.Equal("UPDATE user SET active = $p0 WHERE email = $p1", cmd.Sql);
    }

    [Fact]
    public void Build_without_Set_clauses_throws_InvalidOperationException()
    {
        var b = SurrealQuery.Update("user");
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }
}
