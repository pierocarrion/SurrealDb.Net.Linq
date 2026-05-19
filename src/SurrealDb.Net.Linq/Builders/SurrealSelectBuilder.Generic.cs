using System.Linq.Expressions;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Generic-typed wrapper around <see cref="SurrealSelectBuilder"/>. Adds
/// <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c> overloads for <c>Where</c>,
/// <c>And</c>, and <c>Or</c>; every other method delegates to the non-generic
/// builder and returns <c>this</c> so the fluent chain stays typed.
/// </summary>
public sealed class SurrealSelectBuilder<T>
{
    private readonly SurrealSelectBuilder _inner;

    internal SurrealSelectBuilder(string target, bool isLive)
    {
        _inner = new SurrealSelectBuilder(target, isLive);
    }

    public SurrealSelectBuilder<T> Where(Expression<Func<T, bool>> predicate)
    {
        _inner.AddRawWhere(ExpressionToWhere.Translate(predicate, _inner.Bag), conjunction: "AND");
        return this;
    }

    public SurrealSelectBuilder<T> And(Expression<Func<T, bool>> predicate) => Where(predicate);

    public SurrealSelectBuilder<T> Or(Expression<Func<T, bool>> predicate)
    {
        _inner.AddRawWhere(ExpressionToWhere.Translate(predicate, _inner.Bag), conjunction: "OR");
        return this;
    }

    public SurrealSelectBuilder<T> Where(string field, SurrealOperator op, object? value = null) { _inner.Where(field, op, value); return this; }
    public SurrealSelectBuilder<T> And(string field, SurrealOperator op, object? value = null) { _inner.And(field, op, value); return this; }
    public SurrealSelectBuilder<T> Or(string field, SurrealOperator op, object? value = null) { _inner.Or(field, op, value); return this; }
    public SurrealSelectBuilder<T> WhereRaw(string expression, IDictionary<string, object?>? parameters = null) { _inner.WhereRaw(expression, parameters); return this; }

    public SurrealSelectBuilder<T> Select(params string[] fields) { _inner.Select(fields); return this; }
    public SurrealSelectBuilder<T> Field(string name) { _inner.Field(name); return this; }
    public SurrealSelectBuilder<T> SelectValue(string expression) { _inner.SelectValue(expression); return this; }
    public SurrealSelectBuilder<T> Only() { _inner.Only(); return this; }
    public SurrealSelectBuilder<T> OrderBy(string field, SortDirection dir = SortDirection.Asc) { _inner.OrderBy(field, dir); return this; }
    public SurrealSelectBuilder<T> GroupBy(params string[] fields) { _inner.GroupBy(fields); return this; }
    public SurrealSelectBuilder<T> Limit(int n) { _inner.Limit(n); return this; }
    public SurrealSelectBuilder<T> Start(int n) { _inner.Start(n); return this; }
    public SurrealSelectBuilder<T> Fetch(params string[] fields) { _inner.Fetch(fields); return this; }

    public ISurrealCommand Build() => _inner.Build();
}
