using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealQueryTests
{
    [Fact]
    public void Kill_emits_KILL_with_live_id_bound_to_named_parameter()
    {
        var id = Guid.NewGuid();

        var cmd = SurrealQuery.Kill(id);

        Assert.Equal("KILL $live", cmd.Sql);
        Assert.Equal(id, cmd.Parameters["live"]);
    }

    [Fact]
    public void Raw_with_no_parameters_returns_empty_parameter_dictionary()
    {
        var cmd = SurrealQuery.Raw("INFO FOR DB");

        Assert.Equal("INFO FOR DB", cmd.Sql);
        Assert.Empty(cmd.Parameters);
    }

    [Fact]
    public void Raw_copies_provided_parameters_into_the_command()
    {
        var p = new Dictionary<string, object?> { ["author"] = "alice" };

        var cmd = SurrealQuery.Raw("SELECT * FROM $author", p);

        Assert.Equal("SELECT * FROM $author", cmd.Sql);
        Assert.Equal("alice", cmd.Parameters["author"]);
    }

    [Fact]
    public void Raw_does_not_share_the_caller_supplied_dictionary_reference()
    {
        var p = new Dictionary<string, object?> { ["x"] = 1 };
        var cmd = SurrealQuery.Raw("SELECT $x", p);

        p["x"] = 999;

        Assert.Equal(1, cmd.Parameters["x"]);
    }
}
