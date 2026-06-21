using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SurrealDb.Net.Models.Response;
using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealClientExtensionsTests
{
    private static ISurrealDbClient NewClient() => Substitute.For<ISurrealDbClient>();

    private static ISurrealCommand NewCommand(string sql = "SELECT * FROM x") =>
        new SurrealCommand(sql, new Dictionary<string, object?>(), Array.Empty<string>());

    // ────────────────────────────────────────────────────────────────────
    // ExecuteScalarStrictAsync
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScalarStrict_returns_default_when_response_empty()
    {
        var client = NewClient();
        client.RawQuery(default!, default!, default)
              .ReturnsForAnyArgs(SurrealDbResponseFactory.Empty());

        var result = await client.ExecuteScalarStrictAsync<string>(NewCommand());

        Assert.Null(result);
    }

    [Fact]
    public async Task ScalarStrict_throws_InvalidOperationException_when_response_has_errors()
    {
        var client = NewClient();
        client.RawQuery(default!, default!, default)
              .ReturnsForAnyArgs(SurrealDbResponseFactory.WithError("Transaction conflict"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecuteScalarStrictAsync<string>(NewCommand()));

        Assert.Contains("Transaction conflict", ex.Message);
    }

    [Fact]
    public async Task ScalarStrict_propagates_cancellation()
    {
        var client = NewClient();
        client.RawQuery(default!, default!, default)
              .ThrowsForAnyArgs(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.ExecuteScalarStrictAsync<string>(NewCommand()));
    }

    // ────────────────────────────────────────────────────────────────────
    // ExecuteCountStrictAsync
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CountStrict_returns_zero_when_response_empty()
    {
        var client = NewClient();
        client.RawQuery(default!, default!, default)
              .ReturnsForAnyArgs(SurrealDbResponseFactory.Empty());

        var result = await client.ExecuteCountStrictAsync(NewCommand());

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CountStrict_throws_InvalidOperationException_when_response_has_errors()
    {
        var client = NewClient();
        client.RawQuery(default!, default!, default)
              .ReturnsForAnyArgs(SurrealDbResponseFactory.WithError("Unique index violation"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecuteCountStrictAsync(NewCommand()));

        Assert.Contains("Unique index violation", ex.Message);
    }

    [Fact]
    public async Task CountStrict_propagates_cancellation()
    {
        var client = NewClient();
        client.RawQuery(default!, default!, default)
              .ThrowsForAnyArgs(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.ExecuteCountStrictAsync(NewCommand()));
    }

    // ────────────────────────────────────────────────────────────────────
    // ExecuteNoResultAsync — IsTransactionConflict path
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoResult_swallows_Transaction_conflict_silently()
    {
        // Regression: antes del fix del SurrealDbErrorExtractor, el path
        // agarraba la propiedad equivocada y el check nunca matcheaba.
        var client = NewClient();
        client.RawQuery(default!, default!, default)
              .ReturnsForAnyArgs(SurrealDbResponseFactory.WithError("Transaction conflict"));

        // No debe lanzar — el optimistic-concurrency contract de SurrealDB v3.
        await client.ExecuteNoResultAsync(NewCommand());
    }

    [Fact]
    public async Task NoResult_throws_for_non_conflict_errors()
    {
        var client = NewClient();
        client.RawQuery(default!, default!, default)
              .ReturnsForAnyArgs(SurrealDbResponseFactory.WithError("Database index violation"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExecuteNoResultAsync(NewCommand()));

        Assert.Contains("Database index violation", ex.Message);
    }

    // ────────────────────────────────────────────────────────────────────
    // InsertWithIdAsync — validación de argumentos (T5)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InsertWithId_rejects_null_table()
    {
        var client = NewClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.InsertWithIdAsync(null!, new Dictionary<string, object?>(), () => "id"));
    }

    [Fact]
    public async Task InsertWithId_rejects_empty_table()
    {
        var client = NewClient();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.InsertWithIdAsync("   ", new Dictionary<string, object?>(), () => "id"));
    }

    [Fact]
    public async Task InsertWithId_rejects_null_fields()
    {
        var client = NewClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.InsertWithIdAsync("user", null!, () => "id"));
    }

    [Fact]
    public async Task InsertWithId_rejects_null_idGenerator()
    {
        var client = NewClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.InsertWithIdAsync("user", new Dictionary<string, object?>(), null!));
    }

    [Fact]
    public async Task InsertWithId_rejects_empty_id_from_generator()
    {
        var client = NewClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.InsertWithIdAsync("user", new Dictionary<string, object?> { ["x"] = 1 }, () => ""));
    }

    [Fact]
    public async Task InsertWithId_builds_expected_sql_with_backtick_escape()
    {
        // El mock captura el SQL emitido para verificar el backtick-escape
        // de nombres de campo (T5).
        var client = NewClient();
        SurrealDbResponseFactory.Empty();
        client.RawQuery(default!, default!, default)
              .ReturnsForAnyArgs(SurrealDbResponseFactory.Empty());

        await client.InsertWithIdAsync(
            "user",
            new Dictionary<string, object?>
            {
                ["email"] = "x@y.com",
                ["active"] = true,
            },
            () => "id1");

        // Recuperar la llamada recibida
        var calls = client.ReceivedCalls().ToList();
        var rawCall = calls.FirstOrDefault(c => c.GetMethodInfo()?.Name == "RawQuery");
        Assert.NotNull(rawCall);
        var args = rawCall!.GetArguments();
        var sql = (string)args[0]!;
        Assert.Contains("`email` = $email", sql);
        Assert.Contains("`active` = $active", sql);
        Assert.Contains("$__rid", sql);
    }
}
