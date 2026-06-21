using System.Linq.Expressions;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Generic-typed wrapper around <see cref="SurrealUpdateBuilder"/>. Adds
/// <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c> overloads for <c>Where</c> /
/// <c>And</c> / <c>Or</c> and <c>Expression&lt;Func&lt;T, K&gt;&gt;</c>
/// overloads for <c>Set</c> / <c>SetExpr</c> / <c>SetIfPresent</c>.
/// </summary>
public sealed class SurrealUpdateBuilder<T>
{
    private readonly SurrealUpdateBuilder _inner;

    internal SurrealUpdateBuilder(string target, bool upsert)
    {
        _inner = new SurrealUpdateBuilder(target, upsert);
    }

    internal SurrealUpdateBuilder(object recordTarget, bool upsert)
    {
        _inner = new SurrealUpdateBuilder(recordTarget, upsert);
    }

    /// <summary>Set a field (resolved from the lambda) to a parameterized value.</summary>
    public SurrealUpdateBuilder<T> Set<K>(Expression<Func<T, K>> selector, object? value)
    {
        var field = MemberSelectorResolver.ResolveField(selector);
        _inner.Set(field, value);
        return this;
    }

    /// <summary>Set a field (resolved from the lambda) to a raw SurrealQL expression.</summary>
    public SurrealUpdateBuilder<T> SetExpr<K>(Expression<Func<T, K>> selector, string expression)
    {
        var field = MemberSelectorResolver.ResolveField(selector);
        _inner.SetExpr(field, expression);
        return this;
    }

    public SurrealUpdateBuilder<T> SetIfPresent<K>(Expression<Func<T, K>> selector, object? value)
    {
        if (value is null) return this;
        if (value is string s && string.IsNullOrEmpty(s)) return this;
        return Set(selector, value);
    }

    public SurrealUpdateBuilder<T> Where(Expression<Func<T, bool>> predicate)
    {
        _inner.AddRawWhere(ExpressionToWhere.Translate(predicate, _inner.Bag), conjunction: "AND");
        return this;
    }

    public SurrealUpdateBuilder<T> And(Expression<Func<T, bool>> predicate) => Where(predicate);

    public SurrealUpdateBuilder<T> Or(Expression<Func<T, bool>> predicate)
    {
        _inner.AddRawWhere(ExpressionToWhere.Translate(predicate, _inner.Bag), conjunction: "OR");
        return this;
    }

    // String-based overloads
    public SurrealUpdateBuilder<T> Set(string field, object? value) { _inner.Set(field, value); return this; }
    public SurrealUpdateBuilder<T> SetExpr(string field, string expression) { _inner.SetExpr(field, expression); return this; }
    public SurrealUpdateBuilder<T> SetIfPresent(string field, object? value) { _inner.SetIfPresent(field, value); return this; }
    public SurrealUpdateBuilder<T> Where(string field, SurrealOperator op, object? value = null) { _inner.Where(field, op, value); return this; }
    public SurrealUpdateBuilder<T> And(string field, SurrealOperator op, object? value = null) { _inner.And(field, op, value); return this; }
    public SurrealUpdateBuilder<T> Or(string field, SurrealOperator op, object? value = null) { _inner.Or(field, op, value); return this; }
    public SurrealUpdateBuilder<T> WhereRaw(string expression, IDictionary<string, object?>? parameters = null) { _inner.WhereRaw(expression, parameters); return this; }
    public SurrealUpdateBuilder<T> Bind(string name, object? value) { _inner.Bind(name, value); return this; }

    public SurrealUpdateBuilder<T> ReturnBefore() { _inner.ReturnBefore(); return this; }
    public SurrealUpdateBuilder<T> ReturnAfter() { _inner.ReturnAfter(); return this; }
    public SurrealUpdateBuilder<T> ReturnNone() { _inner.ReturnNone(); return this; }
    public SurrealUpdateBuilder<T> ReturnDiff() { _inner.ReturnDiff(); return this; }
    public SurrealUpdateBuilder<T> Return(string expression) { _inner.Return(expression); return this; }
    public SurrealUpdateBuilder<T> Return(SurrealReturn value) { _inner.Return(value); return this; }
    public SurrealUpdateBuilder<T> ReturnFields(params string[] fields) { _inner.ReturnFields(fields); return this; }

    public ISurrealCommand Build() => _inner.Build();
}
