using Xunit;

namespace SurrealDb.Net.Linq.Tests;

/// <summary>
/// Tests for the extended ExpressionToWhere visitor in 0.6.0:
/// DateTime properties → time::* functions, additional string methods,
/// Nullable handling.
/// </summary>
public class VisitorExtensionsTests
{
    private sealed class Row
    {
        public string Email { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int? Age { get; set; }
        public bool Active { get; set; }
    }

    // ── DateTime properties ─────────────────────────────────────────────

    [Fact]
    public void DateTime_Year_renders_time_year()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.CreatedAt.Year == 2024).Build();
        Assert.Contains("time::year(created_at) = $p0", cmd.Sql);
        Assert.Equal(2024, cmd.Parameters["p0"]);
    }

    [Fact]
    public void DateTime_Month_renders_time_month()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.CreatedAt.Month > 6).Build();
        Assert.Contains("time::month(created_at) > $p0", cmd.Sql);
    }

    [Fact]
    public void DateTime_Day_renders_time_day()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.CreatedAt.Day == 15).Build();
        Assert.Contains("time::day(created_at) = $p0", cmd.Sql);
    }

    [Fact]
    public void DateTime_Hour_renders_time_hour()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.CreatedAt.Hour < 12).Build();
        Assert.Contains("time::hour(created_at) < $p0", cmd.Sql);
    }

    [Fact]
    public void DateTime_Date_renders_datetime_date()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.CreatedAt.Date == new DateTime(2025, 1, 1)).Build();
        Assert.Contains("datetime::date(created_at) = $p0", cmd.Sql);
    }

    [Fact]
    public void DateTimeOffset_property_also_resolves()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.UpdatedAt.Year == 2025).Build();
        Assert.Contains("time::year(updated_at) = $p0", cmd.Sql);
    }

    // ── String methods ──────────────────────────────────────────────────

    [Fact]
    public void String_ToLower_renders_string_lowercase()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.Email.ToLower() == "a@x.com").Build();
        Assert.Contains("string::lowercase(email) = $p0", cmd.Sql);
    }

    [Fact]
    public void String_ToUpper_renders_string_uppercase()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.Email.ToUpper() == "A@X.COM").Build();
        Assert.Contains("string::uppercase(email) = $p0", cmd.Sql);
    }

    [Fact]
    public void String_Trim_renders_string_trim()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.Email.Trim() == "a@x.com").Build();
        Assert.Contains("string::trim(email) = $p0", cmd.Sql);
    }

    [Fact]
    public void String_Replace_renders_string_replace()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.Email.Replace("@", "_") == "a_x.com").Build();
        Assert.Contains("string::replace(email, $p0, $p1) = $p2", cmd.Sql);
        Assert.Equal("@", cmd.Parameters["p0"]);
        Assert.Equal("_", cmd.Parameters["p1"]);
        Assert.Equal("a_x.com", cmd.Parameters["p2"]);
    }

    [Fact]
    public void String_IsNullOrWhiteSpace_renders_combined_clause()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => string.IsNullOrWhiteSpace(r.Email)).Build();
        // (email IS NONE OR email = $p0 OR string::trim(email) = $p0)
        Assert.Contains("email IS NONE", cmd.Sql);
        Assert.Contains("string::trim(email) = $p0", cmd.Sql);
        Assert.Equal("", cmd.Parameters["p0"]);
    }

    // ── Nullable<T> handling ────────────────────────────────────────────

    [Fact]
    public void Nullable_HasValue_throws_NotSupportedException()
    {
        // .HasValue is not supported — caller should use r.Age != null pattern.
        Assert.Throws<NotSupportedException>(() =>
            SurrealQuery.From<Row>("t").Where(r => r.Age.HasValue).Build());
    }

    [Fact]
    public void Nullable_not_null_renders_IS_NOT_NONE()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.Age != null).Build();
        Assert.Contains("age IS NOT NONE", cmd.Sql);
    }

    [Fact]
    public void Nullable_equal_value_renders_param()
    {
        var cmd = SurrealQuery.From<Row>("t").Where(r => r.Age == 30).Build();
        Assert.Contains("age = $p0", cmd.Sql);
        Assert.Equal(30, cmd.Parameters["p0"]);
    }
}
