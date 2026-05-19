using System.Text.Json.Serialization;

namespace SurrealDb.Net.Linq.Tests;

internal sealed class User
{
    public string? Email { get; set; }
    public bool Active { get; set; }
    public int Salary { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Department { get; set; }
    public string? Status { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public Address? Address { get; set; }

    [JsonPropertyName("hire_date_custom")]
    public DateTimeOffset HireDate { get; set; }
}

internal sealed class Address
{
    public string? City { get; set; }
    public string? Country { get; set; }
}
