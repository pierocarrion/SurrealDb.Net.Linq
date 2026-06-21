using Xunit;

namespace SurrealDb.Net.Linq.Tests;

/// <summary>
/// Tests de validación de argumentos en builders (T8, v0.4.0). Cubren
/// ArgumentNullException / ArgumentException / ArgumentOutOfRangeException
/// para todos los métodos públicos que aceptan entradas del caller.
/// </summary>
public class BuilderValidationTests
{
    // ── Select builder ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Select_From_rejects_null_or_empty_target(string? target) =>
        Assert.ThrowsAny<ArgumentException>(() => SurrealQuery.From(target!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Select_Live_rejects_null_or_empty_target(string? target) =>
        Assert.ThrowsAny<ArgumentException>(() => SurrealQuery.Live(target!));

    [Fact]
    public void Select_Field_rejects_null() =>
        Assert.ThrowsAny<ArgumentException>(() => SurrealQuery.From("u").Field(null!));

    [Fact]
    public void Select_SelectValue_rejects_null() =>
        Assert.ThrowsAny<ArgumentException>(() => SurrealQuery.From("u").SelectValue(null!));

    [Fact]
    public void Select_Where_rejects_null_field() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.From("u").Where(null!, SurrealOperator.Equals, 1));

    [Fact]
    public void Select_WhereRaw_rejects_null_expression() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.From("u").WhereRaw(null!));

    [Fact]
    public void Select_OrderBy_rejects_null_field() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.From("u").OrderBy(null!));

    [Fact]
    public void Select_Limit_rejects_negative() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurrealQuery.From("u").Limit(-1));

    [Fact]
    public void Select_Start_rejects_negative() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurrealQuery.From("u").Start(-5));

    [Fact]
    public void Select_Limit_accepts_zero() =>
        Assert.NotNull(SurrealQuery.From("u").Limit(0).Build());

    [Fact]
    public void Select_Start_accepts_zero() =>
        Assert.NotNull(SurrealQuery.From("u").Start(0).Build());

    // ── Create builder ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_rejects_null_or_empty_target(string? target) =>
        Assert.ThrowsAny<ArgumentException>(() => SurrealQuery.Create(target!));

    [Fact]
    public void Create_Set_rejects_null_field() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Create("u").Set(null!, 1));

    [Fact]
    public void Create_SetExpr_rejects_null_field() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Create("u").SetExpr(null!, "time::now()"));

    [Fact]
    public void Create_SetExpr_rejects_null_expression() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Create("u").SetExpr("a", null!));

    [Fact]
    public void Create_Bind_rejects_null_name() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Create("u").Bind(null!, 1));

    [Fact]
    public void Create_Return_rejects_null_expression() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Create("u").Set("a", 1).Return(null!));

    // ── Update builder ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Update_rejects_null_or_empty_target(string? target) =>
        Assert.ThrowsAny<ArgumentException>(() => SurrealQuery.Update(target!));

    [Fact]
    public void UpdateRecord_rejects_null_recordId() =>
        Assert.Throws<ArgumentNullException>(() => SurrealQuery.UpdateRecord(null!));

    [Fact]
    public void Update_Set_rejects_null_field() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Update("u").Set(null!, 1));

    [Fact]
    public void Update_SetExpr_rejects_null_field() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Update("u").SetExpr(null!, "time::now()"));

    [Fact]
    public void Update_Where_rejects_null_field() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Update("u").Where(null!, SurrealOperator.Equals, 1));

    [Fact]
    public void Update_WhereRaw_rejects_null_expression() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Update("u").WhereRaw(null!));

    [Fact]
    public void Update_Bind_rejects_null_name() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Update("u").Bind(null!, 1));

    [Fact]
    public void Update_Return_rejects_null_expression() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Update("u").Set("a", 1).Return(null!));

    // ── Upsert builder ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Upsert_rejects_null_or_empty_target(string? target) =>
        Assert.ThrowsAny<ArgumentException>(() => SurrealQuery.Upsert(target!));

    [Fact]
    public void UpsertRecord_rejects_null_recordId() =>
        Assert.Throws<ArgumentNullException>(() => SurrealQuery.UpsertRecord(null!));

    // ── Delete builder ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Delete_rejects_null_or_empty_target(string? target) =>
        Assert.ThrowsAny<ArgumentException>(() => SurrealQuery.Delete(target!));

    [Fact]
    public void Delete_Where_rejects_null_field() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Delete("u").Where(null!, SurrealOperator.Equals, 1));

    [Fact]
    public void Delete_WhereRaw_rejects_null_expression() =>
        Assert.ThrowsAny<ArgumentException>(() =>
            SurrealQuery.Delete("u").WhereRaw(null!));

    // ── Raw escape hatch ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Raw_rejects_null_or_empty_sql(string? sql) =>
        Assert.ThrowsAny<ArgumentException>(() => SurrealQuery.Raw(sql!));
}
