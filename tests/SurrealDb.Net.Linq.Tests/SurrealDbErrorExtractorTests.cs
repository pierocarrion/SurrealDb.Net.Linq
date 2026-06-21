using Xunit;

namespace SurrealDb.Net.Linq.Tests;

public class SurrealDbErrorExtractorTests
{
    // Tipos anónimos simulando RpcErrorResponseContent (la reflexión opera
    // por nombre de propiedad, así que cubrimos el contract sin depender
    // de los tipos internos del SDK).
    private sealed class FakeRpcError
    {
        public string? Message { get; set; }
        public object? Details { get; set; }
        public long Code { get; set; }
    }

    private sealed class FakeDetails
    {
        public string? Kind { get; set; }
        public IReadOnlyDictionary<string, object?>? Details { get; set; }
        public override string ToString() => "fake-details";
    }

    [Fact]
    public void Returns_Message_when_present()
    {
        var errors = new object[] { new FakeRpcError { Message = "Transaction conflict" } };

        var detail = SurrealDbErrorExtractor.GetFirstErrorDetail(errors);

        Assert.Equal("Transaction conflict", detail);
    }

    [Fact]
    public void Falls_back_to_Details_when_Message_is_null()
    {
        var errors = new object[]
        {
            new FakeRpcError { Message = null, Details = "Index violation" }
        };

        var detail = SurrealDbErrorExtractor.GetFirstErrorDetail(errors);

        Assert.Equal("Index violation", detail);
    }

    [Fact]
    public void Falls_back_to_Details_when_Message_is_empty()
    {
        var errors = new object[]
        {
            new FakeRpcError { Message = "", Details = "Field required" }
        };

        var detail = SurrealDbErrorExtractor.GetFirstErrorDetail(errors);

        Assert.Equal("Field required", detail);
    }

    [Fact]
    public void Falls_back_to_Details_ToString_when_Details_is_complex_object()
    {
        var errors = new object[]
        {
            new FakeRpcError { Message = null, Details = new FakeDetails() }
        };

        var detail = SurrealDbErrorExtractor.GetFirstErrorDetail(errors);

        Assert.Equal("fake-details", detail);
    }

    [Fact]
    public void Falls_back_to_ToString_when_no_Message_no_Details()
    {
        var errors = new object[] { "Some plain string error" };

        var detail = SurrealDbErrorExtractor.GetFirstErrorDetail(errors);

        Assert.Equal("Some plain string error", detail);
    }

    [Fact]
    public void Returns_UnknownError_when_errors_empty()
    {
        var detail = SurrealDbErrorExtractor.GetFirstErrorDetail(Array.Empty<object>());

        Assert.Equal("Unknown error", detail);
    }

    [Fact]
    public void IsTransactionConflict_matches_case_insensitive()
    {
        Assert.True(SurrealDbErrorExtractor.IsTransactionConflict("Transaction conflict"));
        Assert.True(SurrealDbErrorExtractor.IsTransactionConflict("TRANSACTION CONFLICT"));
        Assert.True(SurrealDbErrorExtractor.IsTransactionConflict("error: transaction conflict on key X"));
    }

    [Fact]
    public void IsTransactionConflict_returns_false_for_other_messages()
    {
        Assert.False(SurrealDbErrorExtractor.IsTransactionConflict("Index violation"));
        Assert.False(SurrealDbErrorExtractor.IsTransactionConflict(""));
    }
}
