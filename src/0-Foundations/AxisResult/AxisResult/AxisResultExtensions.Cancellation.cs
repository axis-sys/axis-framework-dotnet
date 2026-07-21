namespace Axis;

// Extensions on Task<AxisResult<TValue>> and ValueTask<AxisResult<TValue>> that
// accept CancellationToken-aware delegates. These are the primary surface users
// interact with when building fluent pipelines.
/// <summary>
/// <see cref="CancellationToken"/>-aware extensions over <see cref="Task{TResult}"/>/<see cref="ValueTask{TResult}"/>
/// of <see cref="AxisResult{TValue}"/>: each awaits the incoming task, forwards the token, and delegates
/// to the matching CT-aware instance operator. See the linked member (its canonical, CT-less form) for
/// the operator's success/failure semantics.
/// </summary>
public static class AxisResultCancellationExtensions
{
    #region Task<AxisResult<TValue>> + CancellationToken

    /// <inheritdoc cref="AxisResult{TValue}.Map{TNew}(Func{TValue, TNew})"/>
    public static async Task<AxisResult<TNew>> MapAsync<TValue, TNew>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, Task<TNew>> mapper,
        CancellationToken ct = default)
        => await (await task).MapAsync(mapper, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Then(Func{TValue, AxisResult})"/>
    public static async Task<AxisResult<TValue>> ThenAsync<TValue>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, Task<AxisResult>> next,
        CancellationToken ct = default)
        => await (await task).ThenAsync(next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async Task<AxisResult<TNew>> ThenAsync<TValue, TNew>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, Task<AxisResult<TNew>>> next,
        CancellationToken ct = default)
        => await (await task).ThenAsync(next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.ToAxisResult(Func{TValue, AxisResult})"/>
    public static async Task<AxisResult> ToAxisResultAsync<TValue>(
    this Task<AxisResult<TValue>> task,
    Func<TValue, CancellationToken, Task<AxisResult>> next,
    CancellationToken ct = default)
    => await (await task).ToAxisResultAsync(next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Tap(Action{TValue})"/>
    public static async Task<AxisResult<TValue>> TapAsync<TValue>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, Task> action,
        CancellationToken ct = default)
        => await (await task).TapAsync(action, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, bool}, AxisError)"/>
    public static async Task<AxisResult<TValue>> EnsureAsync<TValue>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, Task<bool>> predicate,
        AxisError error,
        CancellationToken ct = default)
        => await (await task).EnsureAsync(predicate, error, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, AxisResult})"/>
    public static async Task<AxisResult<TValue>> EnsureAsync<TValue>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, Task<AxisResult>> validation,
        CancellationToken ct = default)
        => await (await task).EnsureAsync(validation, ct);

    /// <inheritdoc cref="AxisResult{TValue}.ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public static async Task<AxisResult<TValue>> ThenUnlessAsync<TValue>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, bool> predicate,
        Func<TValue, CancellationToken, Task<AxisResult>> next,
        CancellationToken ct = default)
        => await (await task).ThenUnlessAsync(predicate, next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public static async Task<AxisResult<TValue>> ThenWhenAsync<TValue>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, bool> predicate,
        Func<TValue, CancellationToken, Task<AxisResult<TValue>>> next,
        CancellationToken ct = default)
        => await (await task).ThenWhenAsync(predicate, next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, TNew})"/>
    public static async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, Task<TNew>> mapper,
        CancellationToken ct = default)
        => await (await task).ZipAsync(mapper, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, Task<AxisResult<TNew>>> mapper,
        CancellationToken ct = default)
        => await (await task).ZipAsync(mapper, ct);

    #endregion

    #region ValueTask<AxisResult<TValue>> + CancellationToken

    /// <inheritdoc cref="AxisResult{TValue}.Map{TNew}(Func{TValue, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> MapAsync<TValue, TNew>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, ValueTask<TNew>> mapper,
        CancellationToken ct = default)
        => await (await task).MapAsync(mapper, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Then(Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> ThenAsync<TValue>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, ValueTask<AxisResult>> next,
        CancellationToken ct = default)
        => await (await task).ThenAsync(next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async ValueTask<AxisResult<TNew>> ThenAsync<TValue, TNew>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, ValueTask<AxisResult<TNew>>> next,
        CancellationToken ct = default)
        => await (await task).ThenAsync(next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Then(Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> ActionAsync<TValue>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, ValueTask<AxisResult>> next,
        CancellationToken ct = default)
        => await (await task).ThenAsync(next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Tap(Action{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> TapAsync<TValue>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, ValueTask> action,
        CancellationToken ct = default)
        => await (await task).TapAsync(action, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, bool}, AxisError)"/>
    public static async ValueTask<AxisResult<TValue>> EnsureAsync<TValue>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, ValueTask<bool>> predicate,
        AxisError error,
        CancellationToken ct = default)
        => await (await task).EnsureAsync(predicate, error, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> EnsureAsync<TValue>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, ValueTask<AxisResult>> validation,
        CancellationToken ct = default)
        => await (await task).EnsureAsync(validation, ct);

    /// <inheritdoc cref="AxisResult{TValue}.ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> ThenUnlessAsync<TValue>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, bool> predicate,
        Func<TValue, CancellationToken, ValueTask<AxisResult>> next,
        CancellationToken ct = default)
        => await (await task).ThenUnlessAsync(predicate, next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public static async ValueTask<AxisResult<TValue>> ThenWhenAsync<TValue>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, bool> predicate,
        Func<TValue, CancellationToken, ValueTask<AxisResult<TValue>>> next,
        CancellationToken ct = default)
        => await (await task).ThenWhenAsync(predicate, next, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, TNew})"/>
    public static async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, ValueTask<TNew>> mapper,
        CancellationToken ct = default)
        => await (await task).ZipAsync(mapper, ct);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, CancellationToken, ValueTask<AxisResult<TNew>>> mapper,
        CancellationToken ct = default)
        => await (await task).ZipAsync(mapper, ct);

    #endregion
}
