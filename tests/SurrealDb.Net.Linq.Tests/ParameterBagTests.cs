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

    // ────────────────────────────────────────────────────────────────────
    // Regresión A7 (0.4.0): Snapshot() ahora devuelve una copia defensiva.
    // Antes de 0.4.0 era una referencia viva al diccionario interno.
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Snapshot_does_not_mutate_after_subsequent_Add()
    {
        var bag = new ParameterBag();
        bag.Add("a");
        var snapshot = bag.Snapshot();
        bag.Add("b");

        Assert.Single(snapshot);
        Assert.Equal(2, bag.Snapshot().Count);
    }

    [Fact]
    public void Snapshot_does_not_mutate_after_subsequent_AddNamed()
    {
        var bag = new ParameterBag();
        bag.AddNamed("x", 1);
        var snapshot = bag.Snapshot();
        bag.AddNamed("y", 2);

        Assert.Single(snapshot);
        Assert.Equal(2, bag.Snapshot().Count);
    }

    [Fact]
    public void GetPlaceholders_returns_insertion_order_for_Add()
    {
        var bag = new ParameterBag();
        bag.Add("a");
        bag.Add("b");
        bag.Add("c");

        Assert.Equal(new[] { "p0", "p1", "p2" }, bag.GetPlaceholders());
    }

    [Fact]
    public void GetPlaceholders_does_not_duplicate_on_AddNamed_overwrite()
    {
        var bag = new ParameterBag();
        bag.AddNamed("k", 1);
        bag.AddNamed("k", 2);

        Assert.Equal(new[] { "k" }, bag.GetPlaceholders());
    }
}
