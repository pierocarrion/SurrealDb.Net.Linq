namespace SurrealDb.Net.Linq;

/// <summary>Comparison operators allowed in <c>Where</c>/<c>And</c>/<c>Or</c> clauses.</summary>
public enum SurrealOperator
{
    Equals,
    NotEquals,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    In,
    NotIn,
    Contains,
    ContainsNot,
    Inside,
    Like,
    IsNone,
    IsNotNone,
}
