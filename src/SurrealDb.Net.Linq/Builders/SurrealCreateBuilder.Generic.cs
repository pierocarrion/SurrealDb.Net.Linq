using System.Linq.Expressions;

namespace SurrealDb.Net.Linq;

/// <summary>
/// Generic-typed wrapper around <see cref="SurrealCreateBuilder"/>. Adds
/// <c>Expression&lt;Func&lt;T, K&gt;&gt;</c> overloads for <c>Set</c> /
/// <c>SetExpr</c> / <c>SetIfPresent</c>; other methods delegate to the
/// non-generic builder.
/// </summary>
public sealed class SurrealCreateBuilder<T>
{
    private readonly SurrealCreateBuilder _inner;

    internal SurrealCreateBuilder(string target)
    {
        _inner = new SurrealCreateBuilder(target);
    }

    /// <summary>Set a field (resolved from the lambda) to a parameterized value.</summary>
    public SurrealCreateBuilder<T> Set<K>(Expression<Func<T, K>> selector, object? value)
    {
        var field = MemberSelectorResolver.ResolveField(selector);
        _inner.Set(field, value);
        return this;
    }

    /// <summary>Set a field (resolved from the lambda) to a raw SurrealQL expression.</summary>
    public SurrealCreateBuilder<T> SetExpr<K>(Expression<Func<T, K>> selector, string expression)
    {
        var field = MemberSelectorResolver.ResolveField(selector);
        _inner.SetExpr(field, expression);
        return this;
    }

    /// <summary>Conditional set — only emits when value is non-null and non-empty (string).</summary>
    public SurrealCreateBuilder<T> SetIfPresent<K>(Expression<Func<T, K>> selector, object? value)
    {
        if (value is null) return this;
        if (value is string s && string.IsNullOrEmpty(s)) return this;
        return Set(selector, value);
    }

    // String-based overloads (still typed at the builder level)
    public SurrealCreateBuilder<T> Set(string field, object? value) { _inner.Set(field, value); return this; }
    public SurrealCreateBuilder<T> SetExpr(string field, string expression) { _inner.SetExpr(field, expression); return this; }
    public SurrealCreateBuilder<T> SetIfPresent(string field, object? value) { _inner.SetIfPresent(field, value); return this; }
    public SurrealCreateBuilder<T> Bind(string name, object? value) { _inner.Bind(name, value); return this; }

    public SurrealCreateBuilder<T> ReturnBefore() { _inner.ReturnBefore(); return this; }
    public SurrealCreateBuilder<T> ReturnAfter() { _inner.ReturnAfter(); return this; }
    public SurrealCreateBuilder<T> ReturnNone() { _inner.ReturnNone(); return this; }
    public SurrealCreateBuilder<T> ReturnDiff() { _inner.ReturnDiff(); return this; }
    public SurrealCreateBuilder<T> Return(string expression) { _inner.Return(expression); return this; }
    public SurrealCreateBuilder<T> Return(SurrealReturn value) { _inner.Return(value); return this; }
    public SurrealCreateBuilder<T> ReturnFields(params string[] fields) { _inner.ReturnFields(fields); return this; }

    public ISurrealCommand Build() => _inner.Build();
}
