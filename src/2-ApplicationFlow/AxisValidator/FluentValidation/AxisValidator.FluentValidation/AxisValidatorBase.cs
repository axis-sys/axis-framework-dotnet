using System.Collections;
using System.Linq.Expressions;
using Axis;
using FluentValidation;
using FluentValidation.Results;

namespace AxisValidator;

public class AxisValidatorBase<T> : AbstractValidator<T>, IAxisValidatorBase<T>
{
    public const int DefaultMaxLength = 255;

    public void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode)
    {
        RuleFor(expression)
            .Must(x => x != null && !Equals(x, default(TProperty)) && (x is not string str || !string.IsNullOrWhiteSpace(str)))
            .WithErrorCode(errorCode);
    }

    public void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, Action dependentRules)
    {
        RuleFor(expression)
            .NotNull()
            .WithErrorCode(errorCode);

        var compiled = expression.Compile();
        When(x => compiled(x) != null, dependentRules);
    }

    public void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, Action<TProperty> dependentRules)
    {
        RuleFor(expression)
            .NotNull().WithErrorCode(errorCode)
            .DependentRules(() =>
                RuleFor(expression).Custom((_, context) =>
                {
                    var value = expression.Compile()(context.InstanceToValidate);
                    if (value is not null)
                        dependentRules(value);
                }));
    }

    public void DependentRules<TProperty1, TProperty2>(
        Expression<Func<T, TProperty1?>> expression1, string errorCode1,
        Expression<Func<T, TProperty2?>> expression2, string errorCode2,
        Func<TProperty1, TProperty2, AxisResult> dependentRules)
    {
        var compiled1 = expression1.Compile();
        var compiled2 = expression2.Compile();

        RuleFor(expression1)
            .NotNull().WithErrorCode(errorCode1)
            .DependentRules(() =>
                RuleFor(expression2)
                    .NotNull().WithErrorCode(errorCode2)
                    .DependentRules(() =>
                        RuleFor(expression2).Custom((_, context) =>
                        {
                            var value1 = compiled1(context.InstanceToValidate);
                            var value2 = compiled2(context.InstanceToValidate);
                            if (value1 is null || value2 is null) return;
                            var result = dependentRules(value1, value2);
                            if (result.IsFailure)
                                context.AddFailure(new ValidationFailure
                                {
                                    PropertyName = context.PropertyPath,
                                    ErrorCode = errorCode2,
                                    ErrorMessage = result.Errors.FirstOrDefault()?.Code ?? errorCode2
                                });
                        })));
    }

    public void RequiredGuid7(Expression<Func<T, string?>> expression, string errorCode)
    {
        var compiled = expression.Compile();
        When(x => compiled(x) == null, () => PrivateNotNullOrEmpty(expression, errorCode));
        When(x => compiled(x) != null, () => PrivateNotNullOrEmpty(expression, errorCode)
            .Must(x =>
            {
                if (!Guid.TryParse(x, out var guid))
                    return false;
                return guid.Version == 7;
            }).WithErrorCode(errorCode));
    }

    public void RequiredWithMaxLength(Expression<Func<T, string?>> expression, string errorCode, int? length = DefaultMaxLength)
    {
        PrivateNotNullOrEmpty(expression, errorCode)
            .Must((_, propertyValue) => propertyValue != null && propertyValue.ToString().Length <= length).WithErrorCode(errorCode);
    }

    public void RequiredSlug(Expression<Func<T, string?>> expression, string errorCode, int? length = DefaultMaxLength)
    {
        PrivateNotNullOrEmpty(expression, errorCode)
            .Must((_, propertyValue) => propertyValue != null
                                        && propertyValue.Length <= length
                                        && propertyValue.All(IsValidSlugChar))
            .WithErrorCode(errorCode);
    }

    private static bool IsValidSlugChar(char c)
        => c is (>= 'a' and <= 'z')
              or (>= 'A' and <= 'Z')
              or (>= '0' and <= '9')
              or '-'
              or '_';

    private static bool HasAny(IEnumerable source)
    {
        if (source is ICollection collection)
            return collection.Count > 0;

        var enumerator = source.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    private static int CountOf(IEnumerable source)
    {
        if (source is ICollection collection)
            return collection.Count;

        var count = 0;
        var enumerator = source.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
                count++;
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }

        return count;
    }

    public void RequiredEmail(Expression<Func<T, string?>> expression, string errorCode)
    {
        PrivateNotNullOrEmpty(expression, errorCode)
            .EmailAddress().WithErrorCode(errorCode);
    }

    public void RequiredTryParse(Expression<Func<T, string?>> expression, string errorCode, Func<object?, bool> parse)
    {
        var compiled = expression.Compile();
        When(x => compiled(x) == null, () => PrivateNotNullOrEmpty(expression, errorCode));
        When(x => compiled(x) != null, () => PrivateNotNullOrEmpty(expression, errorCode).Must(x => parse(x)).WithErrorCode(errorCode));
    }

    public void Range<TValue>(
        Expression<Func<T, TValue?>> expression,
        string errorCode,
        TValue? min = null,
        TValue? max = null)
        where TValue : struct, IComparable<TValue>
    {
        var compiled = expression.Compile();
        When(x => compiled(x).HasValue, () =>
            RuleFor(expression)
                .Must(x => x.HasValue
                        && (min == null || x.Value.CompareTo(min.Value) >= 0)
                        && (max == null || x.Value.CompareTo(max.Value) <= 0))
                .WithErrorCode(errorCode));
    }

    public void RequiredCollection<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode)
        where TProperty : IEnumerable
    {
        RuleFor(expression)
            .Must(collection => collection is not null && HasAny(collection))
            .WithErrorCode(errorCode);
    }

    public void MaxCount<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, int max)
        where TProperty : IEnumerable
    {
        RuleFor(expression)
            .Must(collection => collection is null || CountOf(collection) <= max)
            .WithErrorCode(errorCode);
    }

    public void Satisfies<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, Func<TProperty?, bool> predicate)
    {
        RuleFor(expression)
            .Must(value => predicate(value))
            .WithErrorCode(errorCode);
    }

    public void Satisfies<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode, Func<T, TProperty?, bool> predicate)
    {
        RuleFor(expression)
            .Must((instance, value) => predicate(instance, value))
            .WithErrorCode(errorCode);
    }

    public void EachSatisfies<TItem>(Expression<Func<T, IEnumerable<TItem>?>> expression, string errorCode, Func<TItem?, bool> predicate)
    {
        RuleForEach(expression)
            .Must(item => predicate(item))
            .WithErrorCode(errorCode);
    }

    public void EachUsesValidator<TItem>(Expression<Func<T, IEnumerable<TItem>?>> expression, AxisValidatorBase<TItem> itemValidator)
    {
        RuleForEach(expression).SetValidator(itemValidator);
    }

    public void UsesValidator<TProperty>(Expression<Func<T, TProperty?>> expression, AxisValidatorBase<TProperty> validator)
        where TProperty : class
    {
        RuleFor(expression).SetValidator(validator!);
    }

    private IRuleBuilderOptions<T, TProperty> PrivateNotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>> expression, string errorCode)
    {
        return RuleFor(expression)
            .NotNull().WithErrorCode(errorCode)
            .DependentRules(() =>
                RuleFor(expression)
                    .Must(x => x != null
                               && !Equals(x, default(TProperty))
                               && (x is not string str || !string.IsNullOrWhiteSpace(str)))
                    .WithErrorCode(errorCode))!;
    }
}
