namespace Axis;

// CancellationToken-aware overloads for the generic AxisResult<TValue> pipeline.
// These are additive overloads: the existing CT-less signatures are preserved
// for retrocompatibility with consumers that close over the token via lambda.
// Every member below is documented via <inheritdoc cref="..."/> pointing at the operator's
// canonical (CT-less) overload in AxisResult.Functional.cs, which carries the full semantics.
public abstract partial class AxisResult<TValue>
{
    #region Task + CancellationToken

    /// <inheritdoc cref="Map{TNew}(Func{TValue, TNew})"/>
    public async Task<AxisResult<TNew>> MapAsync<TNew>(
        Func<TValue, CancellationToken, Task<TNew>> mapper,
        CancellationToken ct = default)
        => IsSuccess ? Ok(await mapper(Value, ct)) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Then(Func{TValue, AxisResult})"/>
    public async Task<AxisResult<TValue>> ThenAsync(
        Func<TValue, CancellationToken, Task<AxisResult>> next,
        CancellationToken ct = default)
    {
        if (IsFailure) return this;
        var nextResult = await next(Value, ct);
        return nextResult.IsSuccess ? this : PropagateErrors<TValue>(nextResult);
    }

    /// <inheritdoc cref="Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public async Task<AxisResult<TNew>> ThenAsync<TNew>(
        Func<TValue, CancellationToken, Task<AxisResult<TNew>>> next,
        CancellationToken ct = default)
        => IsSuccess ? await next(Value, ct) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="ToAxisResult(Func{TValue, AxisResult})"/>
    public async Task<AxisResult> ToAxisResultAsync(
        Func<TValue, CancellationToken, Task<AxisResult>> next,
        CancellationToken ct = default)
        => IsSuccess ? await next(Value, ct) : PropagateErrors<TValue>(this);

    /// <inheritdoc cref="Tap(Action{TValue})"/>
    public async Task<AxisResult<TValue>> TapAsync(
        Func<TValue, CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        if (IsSuccess) await action(Value, ct);
        return this;
    }

    /// <inheritdoc cref="Ensure(Func{TValue, bool}, AxisError)"/>
    public async Task<AxisResult<TValue>> EnsureAsync(
        Func<TValue, CancellationToken, Task<bool>> predicate,
        AxisError error,
        CancellationToken ct = default)
        => !IsSuccess ? this : (await predicate(Value, ct) ? this : Error<TValue>(error));

    /// <inheritdoc cref="Ensure(Func{TValue, AxisResult})"/>
    public async Task<AxisResult<TValue>> EnsureAsync(
        Func<TValue, CancellationToken, Task<AxisResult>> validation,
        CancellationToken ct = default)
    {
        if (!IsSuccess) return this;
        var r = await validation(Value, ct);
        return r.IsSuccess ? this : PropagateErrors<TValue>(r);
    }

    /// <inheritdoc cref="ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public async Task<AxisResult<TValue>> ThenUnlessAsync(
        Func<TValue, bool> predicate,
        Func<TValue, CancellationToken, Task<AxisResult>> next,
        CancellationToken ct = default)
    {
        if (IsFailure) return this;
        if (predicate(Value)) return this;
        var r = await next(Value, ct);
        return r.IsSuccess ? this : PropagateErrors<TValue>(r);
    }

    /// <inheritdoc cref="ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public async Task<AxisResult<TValue>> ThenWhenAsync(
        Func<TValue, bool> predicate,
        Func<TValue, CancellationToken, Task<AxisResult<TValue>>> next,
        CancellationToken ct = default)
    {
        if (IsFailure) return this;
        return predicate(Value) ? await next(Value, ct) : this;
    }

    /// <inheritdoc cref="Zip{TNew}(Func{TValue, TNew})"/>
    public async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TNew>(
        Func<TValue, CancellationToken, Task<TNew>> mapper,
        CancellationToken ct = default)
        => IsSuccess
            ? Ok<(TValue Value1, TNew Value2)>((Value, await mapper(Value, ct)))
            : PropagateErrors<(TValue Value1, TNew Value2)>(this);

    /// <inheritdoc cref="Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TNew>(
        Func<TValue, CancellationToken, Task<AxisResult<TNew>>> mapper,
        CancellationToken ct = default)
    {
        if (!IsSuccess) return PropagateErrors<(TValue Value1, TNew Value2)>(this);
        var r = await mapper(Value, ct);
        return r.IsSuccess
            ? Ok<(TValue Value1, TNew Value2)>((Value, r.Value))
            : PropagateErrors<(TValue Value1, TNew Value2)>(r);
    }

    #endregion

    #region ValueTask + CancellationToken

    /// <inheritdoc cref="Map{TNew}(Func{TValue, TNew})"/>
    public async ValueTask<AxisResult<TNew>> MapAsync<TNew>(
        Func<TValue, CancellationToken, ValueTask<TNew>> mapper,
        CancellationToken ct = default)
        => IsSuccess ? Ok(await mapper(Value, ct)) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Then(Func{TValue, AxisResult})"/>
    public async ValueTask<AxisResult<TValue>> ThenAsync(
        Func<TValue, CancellationToken, ValueTask<AxisResult>> next,
        CancellationToken ct = default)
    {
        if (IsFailure) return this;
        var nextResult = await next(Value, ct);
        return nextResult.IsSuccess ? this : PropagateErrors<TValue>(nextResult);
    }

    /// <inheritdoc cref="Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public async ValueTask<AxisResult<TNew>> ThenAsync<TNew>(
        Func<TValue, CancellationToken, ValueTask<AxisResult<TNew>>> next,
        CancellationToken ct = default)
        => IsSuccess ? await next(Value, ct) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Tap(Action{TValue})"/>
    public async ValueTask<AxisResult<TValue>> TapAsync(
        Func<TValue, CancellationToken, ValueTask> action,
        CancellationToken ct = default)
    {
        if (IsSuccess) await action(Value, ct);
        return this;
    }

    /// <inheritdoc cref="Ensure(Func{TValue, bool}, AxisError)"/>
    public async ValueTask<AxisResult<TValue>> EnsureAsync(
        Func<TValue, CancellationToken, ValueTask<bool>> predicate,
        AxisError error,
        CancellationToken ct = default)
        => !IsSuccess ? this : (await predicate(Value, ct) ? this : Error<TValue>(error));

    /// <inheritdoc cref="Ensure(Func{TValue, AxisResult})"/>
    public async ValueTask<AxisResult<TValue>> EnsureAsync(
        Func<TValue, CancellationToken, ValueTask<AxisResult>> validation,
        CancellationToken ct = default)
    {
        if (!IsSuccess) return this;
        var r = await validation(Value, ct);
        return r.IsSuccess ? this : PropagateErrors<TValue>(r);
    }

    /// <inheritdoc cref="ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public async ValueTask<AxisResult<TValue>> ThenUnlessAsync(
        Func<TValue, bool> predicate,
        Func<TValue, CancellationToken, ValueTask<AxisResult>> next,
        CancellationToken ct = default)
    {
        if (IsFailure) return this;
        if (predicate(Value)) return this;
        var r = await next(Value, ct);
        return r.IsSuccess ? this : PropagateErrors<TValue>(r);
    }

    /// <inheritdoc cref="ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public async ValueTask<AxisResult<TValue>> ThenWhenAsync(
        Func<TValue, bool> predicate,
        Func<TValue, CancellationToken, ValueTask<AxisResult<TValue>>> next,
        CancellationToken ct = default)
    {
        if (IsFailure) return this;
        return predicate(Value) ? await next(Value, ct) : this;
    }

    /// <inheritdoc cref="Zip{TNew}(Func{TValue, TNew})"/>
    public async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TNew>(
        Func<TValue, CancellationToken, ValueTask<TNew>> mapper,
        CancellationToken ct = default)
        => IsSuccess
            ? Ok<(TValue Value1, TNew Value2)>((Value, await mapper(Value, ct)))
            : PropagateErrors<(TValue Value1, TNew Value2)>(this);

    /// <inheritdoc cref="Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TNew>(
        Func<TValue, CancellationToken, ValueTask<AxisResult<TNew>>> mapper,
        CancellationToken ct = default)
    {
        if (!IsSuccess) return PropagateErrors<(TValue Value1, TNew Value2)>(this);
        var r = await mapper(Value, ct);
        return r.IsSuccess
            ? Ok<(TValue Value1, TNew Value2)>((Value, r.Value))
            : PropagateErrors<(TValue Value1, TNew Value2)>(r);
    }

    #endregion
}
