using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class ParameterBagTests
{
    [Fact]
    public void Add_numbers_placeholders_sequentially_from_p0()
    {
        var bag = new ParameterBag();

        Assert.Equal("$p0", bag.Add("a"));
        Assert.Equal("$p1", bag.Add(42));
        Assert.Equal("$p2", bag.Add(null));
    }

    [Fact]
    public void Snapshot_returns_all_bound_values_under_unprefixed_names()
    {
        var bag = new ParameterBag();
        bag.Add("alice");
        bag.Add(42);

        var snap = bag.Snapshot();

        Assert.Equal(2, snap.Count);
        Assert.Equal("alice", snap["p0"]);
        Assert.Equal(42, snap["p1"]);
    }

    [Fact]
    public void AddNamed_writes_under_caller_supplied_key()
    {
        var bag = new ParameterBag();
        bag.AddNamed("author", "alice");

        var snap = bag.Snapshot();
        Assert.Equal("alice", snap["author"]);
    }

    [Fact]
    public void AddNamed_overwrites_existing_value_for_same_key()
    {
        var bag = new ParameterBag();
        bag.AddNamed("k", "first");
        bag.AddNamed("k", "second");

        Assert.Equal("second", bag.Snapshot()["k"]);
    }
}
