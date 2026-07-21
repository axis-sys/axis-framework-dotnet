namespace Axis.Testing;

/// <summary>
/// Awaitable overloads of the <see cref="AxisResultAssertions"/> helpers, so async test code asserts
/// without wrapping the await in parentheses: <c>await cache.GetAsync&lt;T&gt;(k).ShouldSucceedAsync()</c>
/// instead of <c>(await cache.GetAsync&lt;T&gt;(k)).Value</c>. Each awaits the result and delegates to the
/// synchronous assertion, so failures throw the same rich <c>XunitException</c>.
/// </summary>
public static class AxisResultAsyncAssertions
{
    public static async Task<T> ShouldSucceedAsync<T>(this Task<AxisResult<T>> result, string? because = null)
        => (await result).ShouldSucceed(because);

    public static async Task ShouldSucceedAsync(this Task<AxisResult> result, string? because = null)
        => (await result).ShouldSucceed(because);

    public static async Task<T> ShouldSucceedWithAsync<T>(this Task<AxisResult<T>> result, T expected, string? because = null)
        => (await result).ShouldSucceedWith(expected, because);

    public static async Task<IReadOnlyList<AxisError>> ShouldFailAsync(this Task<AxisResult> result, string? because = null)
        => (await result).ShouldFail(because);

    public static async Task<IReadOnlyList<AxisError>> ShouldFailAsync<T>(this Task<AxisResult<T>> result, string? because = null)
        => (await result).ShouldFail(because);

    public static async Task<AxisError> ShouldFailWithCodeAsync(this Task<AxisResult> result, string expectedCode, string? because = null)
        => (await result).ShouldFailWithCode(expectedCode, because);

    public static async Task<AxisError> ShouldFailWithCodeAsync<T>(this Task<AxisResult<T>> result, string expectedCode, string? because = null)
        => (await result).ShouldFailWithCode(expectedCode, because);

    public static async Task<AxisError> ShouldFailWithTypeAsync(this Task<AxisResult> result, AxisErrorType expectedType, string? because = null)
        => (await result).ShouldFailWithType(expectedType, because);

    public static async Task<AxisError> ShouldFailWithTypeAsync<T>(this Task<AxisResult<T>> result, AxisErrorType expectedType, string? because = null)
        => (await result).ShouldFailWithType(expectedType, because);
}
