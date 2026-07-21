using AxisMediator.Contracts;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AxisValidator.FluentValidation.UnitTests;

public class AxisValidatorBaseTests
{
    // ── Test commands ──────────────────────────────────────────────────────

    private record TestCommand
    {
        public string? Email { get; init; }
        public string? Guid7Id { get; init; }
        public string? Name { get; init; }
    }

    // ── RequiredEmail ──────────────────────────────────────────────────────

    private class EmailValidator : AxisValidatorBase<TestCommand>
    {
        public EmailValidator()
        {
            RequiredEmail(x => x.Email, "EMAIL_NULL_OR_NOT_VALID");
        }
    }

    [Fact]
    public void RequiredEmail_ValidEmail_Passes()
    {
        var validator = new EmailValidator();
        var result = validator.Validate(new TestCommand { Email = "test@example.com" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequiredEmail_NullEmail_Fails()
    {
        var validator = new EmailValidator();
        var result = validator.Validate(new TestCommand { Email = null });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "EMAIL_NULL_OR_NOT_VALID");
    }

    [Fact]
    public void RequiredEmail_InvalidFormat_Fails()
    {
        var validator = new EmailValidator();
        var result = validator.Validate(new TestCommand { Email = "not-an-email" });
        Assert.False(result.IsValid);
    }

    // ── RequiredGuid7 ──────────────────────────────────────────────────────

    private class Guid7Validator : AxisValidatorBase<TestCommand>
    {
        public Guid7Validator()
        {
            RequiredGuid7(x => x.Guid7Id, "GUID7_NULL_OR_NOT_VALID");
        }
    }

    [Fact]
    public void RequiredGuid7_ValidGuid7_Passes()
    {
        var guid7 = Guid.CreateVersion7().ToString();
        var validator = new Guid7Validator();
        var result = validator.Validate(new TestCommand { Guid7Id = guid7 });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequiredGuid7_NullValue_Fails()
    {
        var validator = new Guid7Validator();
        var result = validator.Validate(new TestCommand { Guid7Id = null });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "GUID7_NULL_OR_NOT_VALID");
    }

    [Fact]
    public void RequiredGuid7_NonGuidString_Fails()
    {
        var validator = new Guid7Validator();
        var result = validator.Validate(new TestCommand { Guid7Id = "not-a-guid" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RequiredGuid7_GuidVersion4_Fails()
    {
        var guid4 = Guid.NewGuid().ToString();
        var validator = new Guid7Validator();
        var result = validator.Validate(new TestCommand { Guid7Id = guid4 });
        Assert.False(result.IsValid);
    }

    // ── NotNullOrEmpty with Action dependentRules ──────────────────────────

    private class NotNullOrEmptyActionValidator : AxisValidatorBase<TestCommand>
    {
        public NotNullOrEmptyActionValidator()
        {
            NotNullOrEmpty(x => x.Name, "NAME_NULL_OR_EMPTY",
                () => RequiredEmail(x => x.Email, "EMAIL_NULL_OR_NOT_VALID"));
        }
    }

    [Fact]
    public void NotNullOrEmpty_Action_NullName_FailsNameOnly()
    {
        var validator = new NotNullOrEmptyActionValidator();
        var result = validator.Validate(new TestCommand { Name = null, Email = null });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_NULL_OR_EMPTY");
    }

    [Fact]
    public void NotNullOrEmpty_Action_ValidName_NullEmail_FailsEmail()
    {
        var validator = new NotNullOrEmptyActionValidator();
        var result = validator.Validate(new TestCommand { Name = "John", Email = null });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "EMAIL_NULL_OR_NOT_VALID");
    }

    [Fact]
    public void NotNullOrEmpty_Action_ValidNameAndEmail_Passes()
    {
        var validator = new NotNullOrEmptyActionValidator();
        var result = validator.Validate(new TestCommand { Name = "John", Email = "test@test.com" });
        Assert.True(result.IsValid);
    }

    // ── RequiredWithMaxLength ──────────────────────────────────────────────

    private class MaxLengthValidator : AxisValidatorBase<TestCommand>
    {
        public MaxLengthValidator()
        {
            RequiredWithMaxLength(x => x.Name, "NAME_NULL_OR_TOO_LONG", 10);
        }
    }

    [Fact]
    public void RequiredWithMaxLength_ValidName_Passes()
    {
        var validator = new MaxLengthValidator();
        var result = validator.Validate(new TestCommand { Name = "Short" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequiredWithMaxLength_NullName_FailsWithErrorCode()
    {
        var validator = new MaxLengthValidator();
        var result = validator.Validate(new TestCommand { Name = null });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_NULL_OR_TOO_LONG");
    }

    [Fact]
    public void RequiredWithMaxLength_EmptyName_FailsWithErrorCode()
    {
        var validator = new MaxLengthValidator();
        var result = validator.Validate(new TestCommand { Name = "" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_NULL_OR_TOO_LONG");
    }

    [Fact]
    public void RequiredWithMaxLength_WhitespaceName_FailsWithErrorCode()
    {
        var validator = new MaxLengthValidator();
        var result = validator.Validate(new TestCommand { Name = "   " });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_NULL_OR_TOO_LONG");
    }

    [Fact]
    public void RequiredWithMaxLength_TooLongName_FailsWithErrorCode()
    {
        var validator = new MaxLengthValidator();
        var result = validator.Validate(new TestCommand { Name = new string('a', 11) });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_NULL_OR_TOO_LONG");
    }

    [Fact]
    public void RequiredEmail_EmptyEmail_FailsWithErrorCode()
    {
        var validator = new EmailValidator();
        var result = validator.Validate(new TestCommand { Email = "" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "EMAIL_NULL_OR_NOT_VALID");
    }

    [Fact]
    public void RequiredGuid7_EmptyValue_FailsWithErrorCode()
    {
        var validator = new Guid7Validator();
        var result = validator.Validate(new TestCommand { Guid7Id = "" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "GUID7_NULL_OR_NOT_VALID");
    }

    // ── RequiredSlug ───────────────────────────────────────────────────────

    private class SlugValidator : AxisValidatorBase<TestCommand>
    {
        public SlugValidator()
        {
            RequiredSlug(x => x.Name, "NAME_INVALID", 10);
        }
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("acme-corp")]
    [InlineData("acme_corp")]
    [InlineData("Acme-Cp-1")]
    [InlineData("ABC123")]
    [InlineData("a")]
    public void RequiredSlug_ValidSlug_Passes(string name)
    {
        var validator = new SlugValidator();
        var result = validator.Validate(new TestCommand { Name = name });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequiredSlug_NullName_FailsWithErrorCode()
    {
        var validator = new SlugValidator();
        var result = validator.Validate(new TestCommand { Name = null });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_INVALID");
    }

    [Fact]
    public void RequiredSlug_EmptyName_FailsWithErrorCode()
    {
        var validator = new SlugValidator();
        var result = validator.Validate(new TestCommand { Name = "" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_INVALID");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("acme corp")]
    [InlineData("acme\tcorp")]
    [InlineData(" acme")]
    [InlineData("acme ")]
    public void RequiredSlug_WhitespaceInName_FailsWithErrorCode(string name)
    {
        var validator = new SlugValidator();
        var result = validator.Validate(new TestCommand { Name = name });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_INVALID");
    }

    [Theory]
    [InlineData("acme@corp")]
    [InlineData("acme!")]
    [InlineData("acme.corp")]
    [InlineData("acme#1")]
    [InlineData("acme/corp")]
    [InlineData("acme\\corp")]
    [InlineData("ação")]
    public void RequiredSlug_SpecialCharactersInName_FailsWithErrorCode(string name)
    {
        var validator = new SlugValidator();
        var result = validator.Validate(new TestCommand { Name = name });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_INVALID");
    }

    [Fact]
    public void RequiredSlug_NameTooLong_FailsWithErrorCode()
    {
        var validator = new SlugValidator();
        var result = validator.Validate(new TestCommand { Name = new string('a', 11) });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_INVALID");
    }

    // ── DependentRules<T1, T2> ─────────────────────────────────────────────

    private record DualCommand
    {
        public string? CountryCode { get; init; }
        public string? PhoneNumber { get; init; }
    }

    private class DependentPhoneValidator : AxisValidatorBase<DualCommand>
    {
        public DependentPhoneValidator()
        {
            DependentRules<string, string>(
                x => x.CountryCode,
                "COUNTRY_REQUIRED",
                x => x.PhoneNumber,
                "PHONE_INVALID",
                (country, phone) =>
                {
                    if (country != "BR") return AxisError.ValidationRule("COUNTRY_REQUIRED");
                    return phone.Length == 11 ? AxisResult.Ok() : AxisError.ValidationRule("PHONE_INVALID");
                });
        }
    }

    [Fact]
    public void DependentRules_NullFirstProperty_FailsWithFirstErrorCode()
    {
        var validator = new DependentPhoneValidator();
        var result = validator.Validate(new DualCommand { CountryCode = null, PhoneNumber = "11987654321" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "COUNTRY_REQUIRED");
    }

    [Fact]
    public void DependentRules_NullSecondProperty_FailsWithSecondErrorCode()
    {
        var validator = new DependentPhoneValidator();
        var result = validator.Validate(new DualCommand { CountryCode = "BR", PhoneNumber = null });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "PHONE_INVALID");
    }

    [Fact]
    public void DependentRules_FailsCrossFieldRule_ReturnsSecondErrorCode()
    {
        var validator = new DependentPhoneValidator();
        var result = validator.Validate(new DualCommand { CountryCode = "BR", PhoneNumber = "short" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "PHONE_INVALID");
    }

    [Fact]
    public void DependentRules_CrossFieldFailsForNonBrazil_ReturnsCustomFailure()
    {
        var validator = new DependentPhoneValidator();
        var result = validator.Validate(new DualCommand { CountryCode = "US", PhoneNumber = "11987654321" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void DependentRules_BothValidAndCrossFieldPasses()
    {
        var validator = new DependentPhoneValidator();
        var result = validator.Validate(new DualCommand { CountryCode = "BR", PhoneNumber = "11987654321" });
        Assert.True(result.IsValid);
    }

    // ── RequiredTryParse ───────────────────────────────────────────────────

    private class TryParseValidator : AxisValidatorBase<TestCommand>
    {
        public TryParseValidator()
        {
            RequiredTryParse(x => x.Name, "NAME_INVALID", v => v is string s && int.TryParse(s, out _));
        }
    }

    [Fact]
    public void RequiredTryParse_ValidInteger_Passes()
    {
        var validator = new TryParseValidator();
        var result = validator.Validate(new TestCommand { Name = "42" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequiredTryParse_Null_Fails()
    {
        var validator = new TryParseValidator();
        var result = validator.Validate(new TestCommand { Name = null });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RequiredTryParse_NonInteger_Fails()
    {
        var validator = new TryParseValidator();
        var result = validator.Validate(new TestCommand { Name = "abc" });
        Assert.False(result.IsValid);
    }

    // ── NotNullOrEmpty (simple) ────────────────────────────────────────────

    private class NotNullOrEmptyValidator : AxisValidatorBase<TestCommand>
    {
        public NotNullOrEmptyValidator()
        {
            NotNullOrEmpty(x => x.Name, "NAME_REQUIRED");
        }
    }

    [Fact]
    public void NotNullOrEmpty_NullValue_Fails()
    {
        var validator = new NotNullOrEmptyValidator();
        var result = validator.Validate(new TestCommand { Name = null });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void NotNullOrEmpty_WhitespaceValue_Fails()
    {
        var validator = new NotNullOrEmptyValidator();
        var result = validator.Validate(new TestCommand { Name = "   " });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void NotNullOrEmpty_ValidValue_Passes()
    {
        var validator = new NotNullOrEmptyValidator();
        var result = validator.Validate(new TestCommand { Name = "John" });
        Assert.True(result.IsValid);
    }

    // ── NotNullOrEmpty with Action<TProperty> dependentRules ───────────────

    private class NotNullOrEmptyActionTypedValidator : AxisValidatorBase<TestCommand>
    {
        public NotNullOrEmptyActionTypedValidator()
        {
            NotNullOrEmpty<string>(x => x.Name, "NAME_REQUIRED", value =>
            {
                if (value.Length > 3) RuleFor(x => x.Name).Must(_ => false).WithErrorCode("NAME_TOO_LONG");
            });
        }
    }

    [Fact]
    public void NotNullOrEmpty_ActionTyped_NullValue_Fails()
    {
        var validator = new NotNullOrEmptyActionTypedValidator();
        var result = validator.Validate(new TestCommand { Name = null });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "NAME_REQUIRED");
    }

    [Fact]
    public void NotNullOrEmpty_ActionTyped_ValidValue_InvokesDependent()
    {
        var validator = new NotNullOrEmptyActionTypedValidator();
        var result = validator.Validate(new TestCommand { Name = "OK" });
        Assert.True(result.IsValid);
    }

    // ── FluentValidatorAdapter (sync + async) through DI ───────────────────

    private sealed record AdapterCommand
    {
        public string? Email { get; init; }
    }

    private sealed class AdapterCommandValidator : AxisValidatorBase<AdapterCommand>
    {
        public AdapterCommandValidator()
        {
            RequiredEmail(x => x.Email, "EMAIL_INVALID");
        }
    }

    private static IAxisValidator<AdapterCommand> BuildAdapter()
    {
        var services = new ServiceCollection();
        var mediator = new Mock<IAxisMediator>();
        mediator.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        services.AddSingleton(mediator.Object);
        services.AddAxisValidator(typeof(AdapterCommandValidator).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IAxisValidator<AdapterCommand>>();
    }

    [Fact]
    public async Task FluentValidatorAdapterAsyncReturnsOkOnValid()
    {
        var adapter = BuildAdapter();

        var result = await adapter.ValidateAsync(new AdapterCommand { Email = "ok@example.com" });

        result.ShouldSucceed();
    }

    [Fact]
    public async Task FluentValidatorAdapterAsyncReturnsErrorOnInvalid()
    {
        var adapter = BuildAdapter();

        var result = await adapter.ValidateAsync(new AdapterCommand { Email = null });

        result.ShouldFailWithCode("EMAIL_INVALID");
    }

    [Fact]
    public void FluentValidatorAdapterSyncReturnsOkOnValid()
    {
        var adapter = BuildAdapter();

        var result = adapter.Validate(new AdapterCommand { Email = "ok@example.com" });

        result.ShouldSucceed();
    }

    [Fact]
    public void FluentValidatorAdapterSyncReturnsErrorOnInvalid()
    {
        var adapter = BuildAdapter();

        var result = adapter.Validate(new AdapterCommand { Email = null });

        result.ShouldFailWithCode("EMAIL_INVALID");
    }

    // ── Range<TValue> ──────────────────────────────────────────────────────

    private record RangeCommand
    {
        public int? Value { get; init; }
    }

    private class BoundedRangeValidator : AxisValidatorBase<RangeCommand>
    {
        public BoundedRangeValidator(int? min = null, int? max = null)
        {
            Range(x => x.Value, "VALUE_OUT_OF_RANGE", min, max);
        }
    }

    [Fact]
    public void Range_NullValue_Passes()
    {
        var validator = new BoundedRangeValidator(min: 1, max: 10);
        var result = validator.Validate(new RangeCommand { Value = null });
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Range_ValueBelowMin_FailsWithErrorCode(int value)
    {
        var validator = new BoundedRangeValidator(min: 1, max: 10);
        var result = validator.Validate(new RangeCommand { Value = value });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "VALUE_OUT_OF_RANGE");
    }

    [Fact]
    public void Range_ValueAtMin_Passes()
    {
        var validator = new BoundedRangeValidator(min: 1, max: 10);
        var result = validator.Validate(new RangeCommand { Value = 1 });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Range_ValueWithinBounds_Passes()
    {
        var validator = new BoundedRangeValidator(min: 1, max: 10);
        var result = validator.Validate(new RangeCommand { Value = 5 });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Range_ValueAtMax_Passes()
    {
        var validator = new BoundedRangeValidator(min: 1, max: 10);
        var result = validator.Validate(new RangeCommand { Value = 10 });
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(100)]
    public void Range_ValueAboveMax_FailsWithErrorCode(int value)
    {
        var validator = new BoundedRangeValidator(min: 1, max: 10);
        var result = validator.Validate(new RangeCommand { Value = value });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "VALUE_OUT_OF_RANGE");
    }

    [Fact]
    public void Range_OnlyMin_AtMin_Passes()
    {
        var validator = new BoundedRangeValidator(min: 5);
        var result = validator.Validate(new RangeCommand { Value = 5 });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Range_OnlyMin_BelowMin_FailsWithErrorCode()
    {
        var validator = new BoundedRangeValidator(min: 5);
        var result = validator.Validate(new RangeCommand { Value = 4 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "VALUE_OUT_OF_RANGE");
    }

    [Fact]
    public void Range_OnlyMax_AtMax_Passes()
    {
        var validator = new BoundedRangeValidator(max: 5);
        var result = validator.Validate(new RangeCommand { Value = 5 });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Range_OnlyMax_AboveMax_FailsWithErrorCode()
    {
        var validator = new BoundedRangeValidator(max: 5);
        var result = validator.Validate(new RangeCommand { Value = 6 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "VALUE_OUT_OF_RANGE");
    }

    [Fact]
    public void Range_NoBounds_AnyValue_Passes()
    {
        var validator = new BoundedRangeValidator();
        var result = validator.Validate(new RangeCommand { Value = 42 });
        Assert.True(result.IsValid);
    }

    // ── Collection commands ────────────────────────────────────────────────

    private record CollectionCommand
    {
        public IReadOnlyList<string> Items { get; init; } = [];
    }

    // ── RequiredCollection ─────────────────────────────────────────────────

    private class RequiredCollectionValidator : AxisValidatorBase<CollectionCommand>
    {
        public RequiredCollectionValidator() => RequiredCollection(x => x.Items, "ITEMS_REQUIRED");
    }

    [Fact]
    public void RequiredCollection_WithItems_Passes()
    {
        var validator = new RequiredCollectionValidator();
        var result = validator.Validate(new CollectionCommand { Items = ["a"] });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequiredCollection_Empty_FailsWithErrorCode()
    {
        var validator = new RequiredCollectionValidator();
        var result = validator.Validate(new CollectionCommand { Items = [] });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "ITEMS_REQUIRED");
    }

    [Fact]
    public void RequiredCollection_Null_FailsWithErrorCode()
    {
        var validator = new RequiredCollectionValidator();
        var result = validator.Validate(new CollectionCommand { Items = null! });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "ITEMS_REQUIRED");
    }

    // ── MaxCount ───────────────────────────────────────────────────────────

    private class MaxCountValidator : AxisValidatorBase<CollectionCommand>
    {
        public MaxCountValidator() => MaxCount(x => x.Items, "TOO_MANY", 2);
    }

    [Fact]
    public void MaxCount_WithinLimit_Passes()
    {
        var validator = new MaxCountValidator();
        var result = validator.Validate(new CollectionCommand { Items = ["a", "b"] });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MaxCount_AboveLimit_FailsWithErrorCode()
    {
        var validator = new MaxCountValidator();
        var result = validator.Validate(new CollectionCommand { Items = ["a", "b", "c"] });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "TOO_MANY");
    }

    [Fact]
    public void MaxCount_Null_Passes()
    {
        var validator = new MaxCountValidator();
        var result = validator.Validate(new CollectionCommand { Items = null! });
        Assert.True(result.IsValid);
    }

    // ── Satisfies (predicate over the property) ────────────────────────────

    private class SatisfiesValidator : AxisValidatorBase<CollectionCommand>
    {
        public SatisfiesValidator() =>
            Satisfies(x => x.Items, "ITEMS_DUPLICATED",
                items => items is null || items.Distinct().Count() == items.Count);
    }

    [Fact]
    public void Satisfies_PredicateHolds_Passes()
    {
        var validator = new SatisfiesValidator();
        var result = validator.Validate(new CollectionCommand { Items = ["a", "b"] });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Satisfies_PredicateFails_ReturnsErrorCode()
    {
        var validator = new SatisfiesValidator();
        var result = validator.Validate(new CollectionCommand { Items = ["a", "a"] });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "ITEMS_DUPLICATED");
    }

    // ── Satisfies (predicate over the root + the property) ─────────────────

    private record PrimaryFieldCommand
    {
        public string? Primary { get; init; }
        public IReadOnlyList<string> Fields { get; init; } = [];
    }

    private class RootSatisfiesValidator : AxisValidatorBase<PrimaryFieldCommand>
    {
        public RootSatisfiesValidator() =>
            Satisfies(x => x.Primary, "PRIMARY_UNKNOWN",
                (command, primary) => string.IsNullOrWhiteSpace(primary) || command.Fields.Contains(primary));
    }

    [Fact]
    public void Satisfies_Root_KnownField_Passes()
    {
        var validator = new RootSatisfiesValidator();
        var result = validator.Validate(new PrimaryFieldCommand { Primary = "name", Fields = ["name", "age"] });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Satisfies_Root_UnknownField_ReturnsErrorCode()
    {
        var validator = new RootSatisfiesValidator();
        var result = validator.Validate(new PrimaryFieldCommand { Primary = "ssn", Fields = ["name", "age"] });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "PRIMARY_UNKNOWN");
    }

    [Fact]
    public void Satisfies_Root_NullProperty_Passes()
    {
        var validator = new RootSatisfiesValidator();
        var result = validator.Validate(new PrimaryFieldCommand { Primary = null, Fields = ["name"] });
        Assert.True(result.IsValid);
    }

    // ── EachSatisfies ──────────────────────────────────────────────────────

    private class EachSatisfiesValidator : AxisValidatorBase<CollectionCommand>
    {
        public EachSatisfiesValidator() =>
            EachSatisfies(x => x.Items, "ITEM_INVALID", item => !string.IsNullOrWhiteSpace(item));
    }

    [Fact]
    public void EachSatisfies_AllValid_Passes()
    {
        var validator = new EachSatisfiesValidator();
        var result = validator.Validate(new CollectionCommand { Items = ["a", "b"] });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EachSatisfies_OneInvalid_ReturnsErrorCode()
    {
        var validator = new EachSatisfiesValidator();
        var result = validator.Validate(new CollectionCommand { Items = ["a", "  "] });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "ITEM_INVALID");
    }

    // ── EachUsesValidator ──────────────────────────────────────────────────

    private class ListItemValidator : AxisValidatorBase<string>
    {
        public ListItemValidator() => NotNullOrEmpty(x => x, "ITEM_REQUIRED");
    }

    private class EachUsesValidatorValidator : AxisValidatorBase<CollectionCommand>
    {
        public EachUsesValidatorValidator() => EachUsesValidator(x => x.Items, new ListItemValidator());
    }

    [Fact]
    public void EachUsesValidator_AllValid_Passes()
    {
        var validator = new EachUsesValidatorValidator();
        var result = validator.Validate(new CollectionCommand { Items = ["a", "b"] });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EachUsesValidator_OneInvalidItem_ReturnsItemErrorCode()
    {
        var validator = new EachUsesValidatorValidator();
        var result = validator.Validate(new CollectionCommand { Items = ["a", ""] });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "ITEM_REQUIRED");
    }

    // ── UsesValidator (nested object) ──────────────────────────────────────

    private record HolderCommand
    {
        public InnerObject? Inner { get; init; }
    }

    private record InnerObject
    {
        public string? Name { get; init; }
    }

    private class InnerObjectValidator : AxisValidatorBase<InnerObject>
    {
        public InnerObjectValidator() => NotNullOrEmpty(x => x.Name, "INNER_NAME_REQUIRED");
    }

    private class UsesValidatorValidator : AxisValidatorBase<HolderCommand>
    {
        public UsesValidatorValidator() => UsesValidator(x => x.Inner, new InnerObjectValidator());
    }

    [Fact]
    public void UsesValidator_NullNested_SkipsAndPasses()
    {
        var validator = new UsesValidatorValidator();
        var result = validator.Validate(new HolderCommand { Inner = null });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UsesValidator_PresentNestedInvalid_ReturnsNestedErrorCode()
    {
        var validator = new UsesValidatorValidator();
        var result = validator.Validate(new HolderCommand { Inner = new InnerObject { Name = null } });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "INNER_NAME_REQUIRED");
    }

    [Fact]
    public void UsesValidator_PresentNestedValid_Passes()
    {
        var validator = new UsesValidatorValidator();
        var result = validator.Validate(new HolderCommand { Inner = new InnerObject { Name = "ok" } });
        Assert.True(result.IsValid);
    }
}
