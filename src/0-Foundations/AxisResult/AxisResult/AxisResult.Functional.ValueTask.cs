namespace Axis;

// ValueTask overloads of the operators canonically documented in AxisResult.Functional.cs; every
// member below is a <inheritdoc cref="..."/> pointer at its (sync or Task-async) counterpart.
public abstract partial class AxisResult
{
    #region ValueTask Functional

    /// <inheritdoc cref="Map{TNew}(Func{TNew})"/>
    public async ValueTask<AxisResult<TNew>> MapAsync<TNew>(Func<ValueTask<TNew>> mapper)
        => IsSuccess ? Ok(await mapper()) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Then(Func{AxisResult})"/>
    public async ValueTask<AxisResult> ThenAsync(Func<ValueTask<AxisResult>> next)
        => IsSuccess ? await next() : this;

    /// <inheritdoc cref="Then{TNew}(Func{AxisResult{TNew}})"/>
    public async ValueTask<AxisResult<TNew>> ThenAsync<TNew>(Func<ValueTask<AxisResult<TNew>>> next)
        => IsSuccess ? await next() : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Tap(Action)"/>
    public async ValueTask<AxisResult> TapAsync(Func<ValueTask> action)
    { if (IsSuccess) await action(); return this; }

    /// <inheritdoc cref="TapError(Action{IReadOnlyList{AxisError}})"/>
    public async ValueTask<AxisResult> TapErrorAsync(Func<IReadOnlyList<AxisError>, ValueTask> action)
    { if (IsFailure) await action(Errors); return this; }

    /// <inheritdoc cref="Match{TResult}(Func{TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public async ValueTask<TResult> MatchAsync<TResult>(Func<ValueTask<TResult>> onSuccess, Func<IReadOnlyList<AxisError>, ValueTask<TResult>> onFailure)
        => IsSuccess ? await onSuccess() : await onFailure(Errors);

    /// <inheritdoc cref="MapError(Func{IReadOnlyList{AxisError}, IEnumerable{AxisError}})"/>
    public async ValueTask<AxisResult> MapErrorAsync(Func<IReadOnlyList<AxisError>, ValueTask<IEnumerable<AxisError>>> mapper)
        => IsSuccess ? this : Error(await mapper(Errors));

    /// <inheritdoc cref="OrElse(Func{IReadOnlyList{AxisError}, AxisResult})"/>
    public async ValueTask<AxisResult> OrElseAsync(Func<IReadOnlyList<AxisError>, ValueTask<AxisResult>> fallback)
        => IsSuccess ? this : await fallback(Errors);

    /// <inheritdoc cref="OrElse(Func{IReadOnlyList{AxisError}, AxisResult}, bool)"/>
    public async ValueTask<AxisResult> OrElseAsync(Func<IReadOnlyList<AxisError>, ValueTask<AxisResult>> fallback, bool combineErrors)
    {
        if (IsSuccess) return this;
        var alt = await fallback(Errors);
        if (alt.IsSuccess) return alt;
        return combineErrors ? Error(Errors.Concat(alt.Errors)) : alt;
    }

    #endregion
}

public abstract partial class AxisResult<TValue>
{
    #region ValueTask Functional

    /// <inheritdoc cref="Map{TNew}(Func{TValue, TNew})"/>
    public async ValueTask<AxisResult<TNew>> MapAsync<TNew>(Func<TValue, ValueTask<TNew>> mapper)
        => IsSuccess ? Ok(await mapper(Value)) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Then(Func{TValue, AxisResult})"/>
    public async ValueTask<AxisResult<TValue>> ThenAsync(Func<TValue, ValueTask<AxisResult>> next)
    {
        if (IsFailure) return this;
        var nextResult = await next(Value);
        return nextResult.IsSuccess ? this : PropagateErrors<TValue>(nextResult);
    }

    /// <inheritdoc cref="Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public async ValueTask<AxisResult<TNew>> ThenAsync<TNew>(Func<TValue, ValueTask<AxisResult<TNew>>> next)
        => IsSuccess ? await next(Value) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="AxisResult.Tap(Action)"/>
    public new async ValueTask<AxisResult<TValue>> TapAsync(Func<ValueTask> action)
    { if (IsSuccess) await action(); return this; }

    /// <inheritdoc cref="Tap(Action{TValue})"/>
    public async ValueTask<AxisResult<TValue>> TapAsync(Func<TValue, ValueTask> action)
    { if (IsSuccess) await action(Value); return this; }

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public new async ValueTask<AxisResult<TValue>> TapErrorAsync(Func<IReadOnlyList<AxisError>, ValueTask> action)
    { if (IsFailure) await action(Errors); return this; }

    /// <inheritdoc cref="Ensure(Func{TValue, bool}, AxisError)"/>
    public async ValueTask<AxisResult<TValue>> EnsureAsync(Func<TValue, ValueTask<bool>> predicate, AxisError error)
        => !IsSuccess ? this : (await predicate(Value) ? this : Error<TValue>(error));

    /// <inheritdoc cref="Ensure(Func{TValue, AxisResult})"/>
    public async ValueTask<AxisResult<TValue>> EnsureAsync(Func<TValue, ValueTask<AxisResult>> validation)
    {
        if (!IsSuccess) return this;
        var r = await validation(Value);
        return r.IsSuccess ? this : PropagateErrors<TValue>(r);
    }

    /// <inheritdoc cref="ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public async ValueTask<AxisResult<TValue>> ThenUnlessAsync(Func<TValue, bool> predicate, Func<TValue, ValueTask<AxisResult>> next)
    {
        if (IsFailure) return this;
        if (predicate(Value)) return this;
        var nextResult = await next(Value);
        return nextResult.IsSuccess ? this : PropagateErrors<TValue>(nextResult);
    }

    /// <inheritdoc cref="ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public async ValueTask<AxisResult<TValue>> ThenWhenAsync(Func<TValue, bool> predicate, Func<TValue, ValueTask<AxisResult<TValue>>> next)
    {
        if (IsFailure) return this;
        return predicate(Value) ? await next(Value) : this;
    }

    /// <inheritdoc cref="Zip{TNew}(Func{TValue, TNew})"/>
    public async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TNew>(Func<TValue, ValueTask<TNew>> mapper)
        => IsSuccess ? Ok<(TValue Value1, TNew Value2)>((Value, await mapper(Value))) : PropagateErrors<(TValue Value1, TNew Value2)>(this);

    /// <inheritdoc cref="Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TNew>(Func<TValue, ValueTask<AxisResult<TNew>>> mapper)
    {
        if (!IsSuccess) return PropagateErrors<(TValue Value1, TNew Value2)>(this);
        var r = await mapper(Value);
        return r.IsSuccess
            ? Ok<(TValue Value1, TNew Value2)>((Value, r.Value))
            : PropagateErrors<(TValue Value1, TNew Value2)>(r);
    }

    /// <inheritdoc cref="AxisResult.Match{TResult}(Func{TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public async ValueTask<TResult> MatchAsync<TResult>(Func<TValue, ValueTask<TResult>> onSuccess, Func<IReadOnlyList<AxisError>, ValueTask<TResult>> onFailure)
        => IsSuccess ? await onSuccess(Value) : await onFailure(Errors);

    /// <inheritdoc cref="AxisResult.MapError(Func{IReadOnlyList{AxisError}, IEnumerable{AxisError}})"/>
    public new async ValueTask<AxisResult<TValue>> MapErrorAsync(Func<IReadOnlyList<AxisError>, ValueTask<IEnumerable<AxisError>>> mapper)
        => IsSuccess ? this : Error<TValue>(await mapper(Errors));

    /// <inheritdoc cref="Recover(Func{IReadOnlyList{AxisError}, TValue})"/>
    public async ValueTask<AxisResult<TValue>> RecoverAsync(Func<IReadOnlyList<AxisError>, ValueTask<TValue>> recovery)
        => IsSuccess ? this : Ok(await recovery(Errors));

    /// <inheritdoc cref="Recover(Func{TValue})"/>
    public async ValueTask<AxisResult<TValue>> RecoverAsync(Func<ValueTask<TValue>> recovery)
        => IsSuccess ? this : Ok(await recovery());

    /// <inheritdoc cref="RecoverWhen(Func{IReadOnlyList{AxisError}, bool}, Func{IReadOnlyList{AxisError}, TValue})"/>
    public async ValueTask<AxisResult<TValue>> RecoverWhenAsync(Func<IReadOnlyList<AxisError>, bool> predicate, Func<IReadOnlyList<AxisError>, ValueTask<TValue>> recovery)
        => IsSuccess ? this : (predicate(Errors) ? Ok(await recovery(Errors)) : this);

    /// <inheritdoc cref="RecoverWhen(AxisErrorType, Func{TValue})"/>
    public async ValueTask<AxisResult<TValue>> RecoverWhenAsync(AxisErrorType type, Func<ValueTask<TValue>> recovery)
        => IsSuccess ? this : (Errors.Any(e => e.Type == type) ? Ok(await recovery()) : this);

    /// <inheritdoc cref="RecoverWhen(string, Func{TValue})"/>
    public async ValueTask<AxisResult<TValue>> RecoverWhenAsync(string code, Func<ValueTask<TValue>> recovery)
        => IsSuccess ? this : (Errors.Any(e => e.Code == code) ? Ok(await recovery()) : this);

    /// <inheritdoc cref="RecoverNotFound(Func{TValue})"/>
    public async ValueTask<AxisResult<TValue>> RecoverNotFoundAsync(Func<ValueTask<TValue>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.NotFound) ? Ok(await recovery()) : this);

    /// <inheritdoc cref="RecoverNotFound(Func{AxisResult{TValue}})"/>
    public async ValueTask<AxisResult<TValue>> RecoverNotFoundAsync(Func<ValueTask<AxisResult<TValue>>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.NotFound) ? await recovery() : this);

    /// <inheritdoc cref="RecoverConflict(Func{TValue})"/>
    public async ValueTask<AxisResult<TValue>> RecoverConflictAsync(Func<ValueTask<TValue>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.Conflict) ? Ok(await recovery()) : this);

    /// <inheritdoc cref="RecoverConflict(Func{AxisResult{TValue}})"/>
    public async ValueTask<AxisResult<TValue>> RecoverConflictAsync(Func<ValueTask<AxisResult<TValue>>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.Conflict) ? await recovery() : this);

    /// <inheritdoc cref="ElseNotFound{TNew}(Func{TValue, TNew}, Func{TNew})"/>
    public async ValueTask<AxisResult<TNew>> ElseNotFoundAsync<TNew>(Func<TValue, ValueTask<TNew>> map, Func<ValueTask<TNew>> recovery)
        => IsSuccess ? Ok(await map(Value)) : (Errors.All(e => e.Type == AxisErrorType.NotFound) ? Ok(await recovery()) : PropagateErrors<TNew>(this));

    /// <inheritdoc cref="AxisResult.OrElse(Func{IReadOnlyList{AxisError}, AxisResult})"/>
    public async ValueTask<AxisResult<TValue>> OrElseAsync(Func<IReadOnlyList<AxisError>, ValueTask<AxisResult<TValue>>> fallback)
        => IsSuccess ? this : await fallback(Errors);

    /// <inheritdoc cref="AxisResult.OrElse(Func{IReadOnlyList{AxisError}, AxisResult}, bool)"/>
    public async ValueTask<AxisResult<TValue>> OrElseAsync(Func<IReadOnlyList<AxisError>, ValueTask<AxisResult<TValue>>> fallback, bool combineErrors)
    {
        if (IsSuccess) return this;
        var alt = await fallback(Errors);
        if (alt.IsSuccess) return alt;
        return combineErrors ? Error<TValue>(Errors.Concat(alt.Errors)) : alt;
    }

    /// <inheritdoc cref="SelectMany{TIntermediate, TNew}(Func{TValue, AxisResult{TIntermediate}}, Func{TValue, TIntermediate, TNew})"/>
    public async ValueTask<AxisResult<TNew>> SelectManyAsync<TIntermediate, TNew>(
        Func<TValue, ValueTask<AxisResult<TIntermediate>>> binder,
        Func<TValue, TIntermediate, TNew> projector)
    {
        if (!IsSuccess) return PropagateErrors<TNew>(this);
        var inner = await binder(Value);
        return inner.IsSuccess
            ? Ok(projector(Value, inner.Value))
            : PropagateErrors<TNew>(inner);
    }

    #endregion
}
