using System.Collections;
using System.Linq.Expressions;
using Axis;

namespace AxisValidator;

public interface IAxisValidatorBase<T>
{
    public const int DefaultMaxLength = 255;

    public void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode);

    public void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, Action dependentRules);

    public void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, Action<TProperty> dependentRules);

    public void DependentRules<TProperty1, TProperty2>(
        Expression<Func<T, TProperty1?>> expression1, string errorCode1,
        Expression<Func<T, TProperty2?>> expression2, string errorCode2,
        Func<TProperty1, TProperty2, AxisResult> dependentRules);

    public void RequiredGuid7(Expression<Func<T, string?>> expression, string errorCode);

    public void RequiredWithMaxLength(Expression<Func<T, string?>> expression, string errorCode, int? length = DefaultMaxLength);

    public void RequiredSlug(Expression<Func<T, string?>> expression, string errorCode, int? length = DefaultMaxLength);

    public void RequiredEmail(Expression<Func<T, string?>> expression, string errorCode);

    public void RequiredTryParse(Expression<Func<T, string?>> expression, string errorCode, Func<object?, bool> parse);

    public void Range<TValue>(
        Expression<Func<T, TValue?>> expression,
        string errorCode,
        TValue? min = null,
        TValue? max = null)
        where TValue : struct, IComparable<TValue>;

    public void RequiredCollection<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode)
        where TProperty : IEnumerable;

    public void MaxCount<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, int max)
        where TProperty : IEnumerable;

    public void Satisfies<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, Func<TProperty?, bool> predicate);

    public void Satisfies<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, Func<T, TProperty?, bool> predicate);

    public void EachSatisfies<TItem>(Expression<Func<T, IEnumerable<TItem>?>> expression, string errorCode, Func<TItem?, bool> predicate);
}
