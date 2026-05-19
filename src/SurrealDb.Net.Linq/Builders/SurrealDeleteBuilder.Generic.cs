using System.Linq.Expressions;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Generic-typed wrapper around <see cref="SurrealDeleteBuilder"/>. Adds
/// <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c> overloads for <c>Where</c>,
/// <c>And</c>, and <c>Or</c>; other methods delegate to the non-generic
/// builder.
/// </summary>
public sealed class SurrealDeleteBuilder<T>
{
    private readonly SurrealDeleteBuilder _inner;

    internal SurrealDeleteBuilder(string target)
    {
        _inner = new SurrealDeleteBuilder(target);
    }

    public SurrealDeleteBuilder<T> Where(Expression<Func<T, bool>> predicate)
    {
        _inner.AddRawWhere(ExpressionToWhere.Translate(predicate, _inner.Bag), conjunction: "AND");
        return this;
    }

    public SurrealDeleteBuilder<T> And(Expression<Func<T, bool>> predicate) => Where(predicate);

    public SurrealDeleteBuilder<T> Or(Expression<Func<T, bool>> predicate)
    {
        _inner.AddRawWhere(ExpressionToWhere.Translate(predicate, _inner.Bag), conjunction: "OR");
        return this;
    }

    public SurrealDeleteBuilder<T> Where(string field, SurrealOperator op, object? value = null) { _inner.Where(field, op, value); return this; }
    public SurrealDeleteBuilder<T> And(string field, SurrealOperator op, object? value = null) { _inner.And(field, op, value); return this; }
    public SurrealDeleteBuilder<T> Or(string field, SurrealOperator op, object? value = null) { _inner.Or(field, op, value); return this; }
    public SurrealDeleteBuilder<T> WhereRaw(string expression, IDictionary<string, object?>? parameters = null) { _inner.WhereRaw(expression, parameters); return this; }

    public SurrealDeleteBuilder<T> ReturnBefore() { _inner.ReturnBefore(); return this; }
    public SurrealDeleteBuilder<T> ReturnDiff() { _inner.ReturnDiff(); return this; }
    public SurrealDeleteBuilder<T> ReturnNone() { _inner.ReturnNone(); return this; }

    public ISurrealCommand Build() => _inner.Build();
}
