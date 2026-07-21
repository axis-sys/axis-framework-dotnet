using Xunit;
using Xunit.Sdk;

namespace Axis.Testing;

/// <summary>
/// Fluent, <see cref="AxisResult"/>-aware assertions for xUnit. Extract values through these helpers
/// instead of reading <c>.Value</c> (which the ROP analyzer flags as AXIS0001): they delegate the
/// inspection to the framework-agnostic <see cref="AxisAssert"/> core, throw a native
/// <see cref="XunitException"/> on failure (so the message renders in the Visual Studio, VS Code and
/// Rider test explorers), and delegate value comparisons to xUnit's own <see cref="Assert"/> for its
/// rich expected/actual diff.
/// </summary>
public static class AxisResultAssertions
{
    /// <summary>Asserts success and returns the value; fails listing the errors otherwise.</summary>
    public static T ShouldSucceed<T>(this AxisResult<T> result, string? because = null)
    {
        if (!AxisAssert.TrySucceed(result, because, out var value, out var message))
            throw new XunitException(message);
        return value;
    }

    /// <summary>Asserts success on a valueless result; fails listing the errors otherwise.</summary>
    public static void ShouldSucceed(this AxisResult result, string? because = null)
    {
        if (!AxisAssert.TrySucceed(result, because, out var message))
            throw new XunitException(message);
    }

    /// <summary>Asserts success and that the value equals <paramref name="expected"/> (xUnit diff on mismatch).</summary>
    public static T ShouldSucceedWith<T>(this AxisResult<T> result, T expected, string? because = null)
    {
        var value = result.ShouldSucceed(because);
        Assert.Equal(expected, value);
        return value;
    }

    /// <summary>Asserts failure and returns the errors; fails otherwise.</summary>
    public static IReadOnlyList<AxisError> ShouldFail(this AxisResult result, string? because = null)
    {
        if (!AxisAssert.TryFail(result, because, out var errors, out var message))
            throw new XunitException(message);
        return errors;
    }

    /// <summary>Asserts failure carrying an error with <paramref name="expectedCode"/>; returns that error.</summary>
    public static AxisError ShouldFailWithCode(this AxisResult result, string expectedCode, string? because = null)
    {
        var errors = result.ShouldFail(because);
        foreach (var error in errors)
            if (error.Code == expectedCode)
                return error;
        throw new XunitException(AxisAssert.MissingCodeMessage(expectedCode, errors, because));
    }

    /// <summary>Asserts failure carrying an error of <paramref name="expectedType"/>; returns that error.</summary>
    public static AxisError ShouldFailWithType(this AxisResult result, AxisErrorType expectedType, string? because = null)
    {
        var errors = result.ShouldFail(because);
        foreach (var error in errors)
            if (error.Type == expectedType)
                return error;
        throw new XunitException(AxisAssert.MissingTypeMessage(expectedType, errors, because));
    }

    /// <summary>Asserts the value is non-null and returns it non-nullable (chainable).</summary>
    public static T ShouldNotBeNull<T>(this T? value, string? because = null)
        where T : class
    {
        if (value is null)
            throw new XunitException(AxisAssert.NotNullMessage(because));
        return value;
    }
}
