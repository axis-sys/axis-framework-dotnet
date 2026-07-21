namespace Axis;

public abstract partial class AxisResult
{
    #region Functional

    /// <summary>
    /// Transforms into a value-carrying success by running <paramref name="mapper"/> — a step that
    /// CANNOT fail. Short-circuits (propagates the errors, unchanged) when this is already a failure.
    /// For a step that can itself fail, use <see cref="Then{TNew}(Func{AxisResult{TNew}})"/> instead.
    /// </summary>
    public AxisResult<TNew> Map<TNew>(Func<TNew> mapper) => IsSuccess ? Ok(mapper()) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Map{TNew}(Func{TNew})"/>
    public async Task<AxisResult<TNew>> MapAsync<TNew>(Func<Task<TNew>> mapper) => IsSuccess ? Ok(await mapper()) : PropagateErrors<TNew>(this);

    /// <summary>
    /// Chains a fallible step that produces no value. Valueless form: runs <paramref name="next"/> only
    /// on success and returns <c>this</c> unchanged when it also succeeds — the upstream is what
    /// downstream operators still see. Short-circuits on the first failure, from either side.
    /// </summary>
    public AxisResult Then(Func<AxisResult> next) => IsSuccess ? next() : this;

    /// <summary>
    /// Chains a fallible step that produces a value, switching the track from valueless to
    /// value-carrying. Runs <paramref name="next"/> only on success; short-circuits (propagating the
    /// errors) on failure, from either side.
    /// </summary>
    public AxisResult<TNew> Then<TNew>(Func<AxisResult<TNew>> next) => IsSuccess ? next() : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Then(Func{AxisResult})"/>
    public async Task<AxisResult> ThenAsync(Func<Task<AxisResult>> next) => IsSuccess ? await next() : this;

    /// <inheritdoc cref="Then{TNew}(Func{AxisResult{TNew}})"/>
    public async Task<AxisResult<TNew>> ThenAsync<TNew>(Func<Task<AxisResult<TNew>>> next) => IsSuccess ? await next() : PropagateErrors<TNew>(this);

    /// <summary>Runs a side effect on success only, without altering the track. Always returns <c>this</c>.</summary>
    public AxisResult Tap(Action action) { if (IsSuccess) action(); return this; }

    /// <inheritdoc cref="Tap(Action)"/>
    public async Task<AxisResult> TapAsync(Func<Task> action) { if (IsSuccess) await action(); return this; }

    /// <summary>Runs a side effect on failure only (e.g. logging), without altering the track. Always returns <c>this</c>. <c>virtual</c> so <see cref="AxisResult{TValue}"/> can override the return type.</summary>
    public virtual AxisResult TapError(Action<IReadOnlyList<AxisError>> action) { if (IsFailure) action(Errors); return this; }

    /// <inheritdoc cref="TapError(Action{IReadOnlyList{AxisError}})"/>
    public virtual async Task<AxisResult> TapErrorAsync(Func<IReadOnlyList<AxisError>, Task> action) { if (IsFailure) await action(Errors); return this; }

    /// <summary>Terminal operator: collapses the pipeline into a single <typeparamref name="TResult"/>, running exactly one of <paramref name="onSuccess"/>/<paramref name="onFailure"/>. Typical exit point (e.g. into an HTTP response).</summary>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<IReadOnlyList<AxisError>, TResult> onFailure) => IsSuccess ? onSuccess() : onFailure(Errors);

    /// <inheritdoc cref="Match{TResult}(Func{TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public async Task<TResult> MatchAsync<TResult>(Func<Task<TResult>> onSuccess, Func<IReadOnlyList<AxisError>, Task<TResult>> onFailure) => IsSuccess ? await onSuccess() : await onFailure(Errors);

    /// <summary>
    /// Rewrites the error list on failure — typically when crossing a boundary that needs different
    /// codes/types than the upstream layer produced. A no-op on success. <c>virtual</c> so
    /// <see cref="AxisResult{TValue}"/> can override the return type.
    /// </summary>
    public virtual AxisResult MapError(Func<IReadOnlyList<AxisError>, IEnumerable<AxisError>> mapper)
        => IsSuccess ? this : Error(mapper(Errors));

    /// <summary>Per-error convenience overload of <see cref="MapError(Func{IReadOnlyList{AxisError}, IEnumerable{AxisError}})"/>: applies <paramref name="mapper"/> to each error individually instead of the whole list.</summary>
    public AxisResult MapError(Func<AxisError, AxisError> mapper)
        => MapError(errors => errors.Select(mapper));

    /// <inheritdoc cref="MapError(Func{IReadOnlyList{AxisError}, IEnumerable{AxisError}})"/>
    public virtual async Task<AxisResult> MapErrorAsync(Func<IReadOnlyList<AxisError>, Task<IEnumerable<AxisError>>> mapper)
        => IsSuccess ? this : Error(await mapper(Errors));

    /// <summary>
    /// On failure, tries an alternative producer instead of short-circuiting; passes success through
    /// unchanged. Unlike <c>Recover*</c>, the alternative is itself an <see cref="AxisResult"/> (may
    /// also fail) rather than a plain value.
    /// </summary>
    public AxisResult OrElse(Func<IReadOnlyList<AxisError>, AxisResult> fallback)
        => IsSuccess ? this : fallback(Errors);

    /// <inheritdoc cref="OrElse(Func{IReadOnlyList{AxisError}, AxisResult})"/>
    public async Task<AxisResult> OrElseAsync(Func<IReadOnlyList<AxisError>, Task<AxisResult>> fallback)
        => IsSuccess ? this : await fallback(Errors);

    /// <summary>
    /// Overload of <see cref="OrElse(Func{IReadOnlyList{AxisError}, AxisResult})"/> that also controls
    /// what happens when the alternative itself fails: with <paramref name="combineErrors"/> true, the
    /// original and alternative error lists are concatenated instead of only the alternative's errors
    /// being kept.
    /// </summary>
    public AxisResult OrElse(Func<IReadOnlyList<AxisError>, AxisResult> fallback, bool combineErrors)
    {
        if (IsSuccess) return this;
        var alt = fallback(Errors);
        if (alt.IsSuccess) return alt;
        return combineErrors ? Error(Errors.Concat(alt.Errors)) : alt;
    }

    /// <inheritdoc cref="OrElse(Func{IReadOnlyList{AxisError}, AxisResult}, bool)"/>
    public async Task<AxisResult> OrElseAsync(Func<IReadOnlyList<AxisError>, Task<AxisResult>> fallback, bool combineErrors)
    {
        if (IsSuccess) return this;
        var alt = await fallback(Errors);
        if (alt.IsSuccess) return alt;
        return combineErrors ? Error(Errors.Concat(alt.Errors)) : alt;
    }

    /// <summary>
    /// Expresses a create-must-not-exist existence check on the rail: a success (the entity was found)
    /// becomes the given <paramref name="errorIfFound"/> failure; a failure whose errors are ALL
    /// <see cref="AxisErrorType.NotFound"/> flips to a valueless success (the miss was expected);
    /// any other failure propagates unchanged. Declared on the non-generic <see cref="AxisResult"/>, so
    /// the result carries no value — re-attach the value to create with an extension such as
    /// <c>WithValueAsync</c> before continuing the pipeline.
    /// </summary>
    public AxisResult RequireNotFound(AxisError errorIfFound)
        => IsSuccess
            ? Error(errorIfFound)
            : Errors.All(e => e.Type == AxisErrorType.NotFound) ? Ok() : this;

    #endregion
}

public abstract partial class AxisResult<TValue>
{
    #region Functional

    /// <summary>Transforms <see cref="Value"/> via <paramref name="mapper"/> — a step that CANNOT fail. Short-circuits on failure. For a step that can itself fail, use <see cref="Then{TNew}(Func{TValue, AxisResult{TNew}})"/> instead.</summary>
    public AxisResult<TNew> Map<TNew>(Func<TValue, TNew> mapper) => IsSuccess ? Ok(mapper(Value)) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Map{TNew}(Func{TValue, TNew})"/>
    public async Task<AxisResult<TNew>> MapAsync<TNew>(Func<TValue, Task<TNew>> mapper) => IsSuccess ? Ok(await mapper(Value)) : PropagateErrors<TNew>(this);

    /// <summary>
    /// Chains a fallible step that produces no value (valueless form). Runs <paramref name="next"/>
    /// with the current value only on success; on the step's own success PRESERVES the upstream value
    /// — the value seen by the next operator is still the original <typeparamref name="TValue"/>, not
    /// whatever <paramref name="next"/> computed. Short-circuits (propagating errors) on either side's
    /// failure. Missing this "value preserved" behavior is the most common Railway-Oriented Programming
    /// mistake — there is no need to hold the value in a local variable across a side-effecting step.
    /// </summary>
    public AxisResult<TValue> Then(Func<TValue, AxisResult> next)
    {
        if (IsFailure) return this;
        var nextResult = next(Value);
        return nextResult.IsSuccess ? this : PropagateErrors<TValue>(nextResult);
    }

    /// <summary>Chains a fallible step that REPLACES the value with a new one, produced from the current value. Runs <paramref name="next"/> only on success; short-circuits (propagating errors) on either side's failure.</summary>
    public AxisResult<TNew> Then<TNew>(Func<TValue, AxisResult<TNew>> next) => IsSuccess ? next(Value) : PropagateErrors<TNew>(this);

    /// <inheritdoc cref="Then(Func{TValue, AxisResult})"/>
    public async Task<AxisResult<TValue>> ThenAsync(Func<TValue, Task<AxisResult>> next)
    {
        if (IsFailure) return this;
        var nextResult = await next(Value);
        return nextResult.IsSuccess ? this : PropagateErrors<TValue>(nextResult);
    }

    /// <inheritdoc cref="Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public async Task<AxisResult<TNew>> ThenAsync<TNew>(Func<TValue, Task<AxisResult<TNew>>> next) => IsSuccess ? await next(Value) : PropagateErrors<TNew>(this);

    /// <summary>Drops down to the valueless <see cref="AxisResult"/> track by running a final fallible step on the value. Short-circuits (propagating errors) on either side's failure.</summary>
    public AxisResult ToAxisResult(Func<TValue, AxisResult> next) => IsSuccess ? next(Value) : PropagateErrors<TValue>(this);

    /// <summary>Drops down to the valueless <see cref="AxisResult"/> track, discarding the value on success. A no-op on the failure rail (errors propagate).</summary>
    public AxisResult ToAxisResult() => IsSuccess ? Ok() : PropagateErrors<TValue>(this);

    /// <inheritdoc cref="ToAxisResult(Func{TValue, AxisResult})"/>
    public async Task<AxisResult> ToAxisResultAsync(Func<TValue, Task<AxisResult>> next) => IsSuccess ? await next(Value) : PropagateErrors<TValue>(this);

    /// <inheritdoc cref="ToAxisResult()"/>
    public async Task<AxisResult> ToAxisResultAsync() => IsSuccess ? Ok() : PropagateErrors<TValue>(this);

    /// <inheritdoc cref="AxisResult.Tap(Action)"/>
    public new AxisResult<TValue> Tap(Action action) { if (IsSuccess) action(); return this; }

    /// <summary>Runs a side effect on success only, passing the value in. Always returns <c>this</c> unchanged.</summary>
    public AxisResult<TValue> Tap(Action<TValue> action) { if (IsSuccess) action(Value); return this; }

    /// <inheritdoc cref="AxisResult.Tap(Action)"/>
    public new async Task<AxisResult<TValue>> TapAsync(Func<Task> action) { if (IsSuccess) await action(); return this; }

    /// <inheritdoc cref="Tap(Action{TValue})"/>
    public async Task<AxisResult<TValue>> TapAsync(Func<TValue, Task> action) { if (IsSuccess) await action(Value); return this; }

    /// <inheritdoc/>
    public override AxisResult<TValue> TapError(Action<IReadOnlyList<AxisError>> action) { if (IsFailure) action(Errors); return this; }

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public new async Task<AxisResult<TValue>> TapErrorAsync(Func<IReadOnlyList<AxisError>, Task> action) { if (IsFailure) await action(Errors); return this; }

    /// <summary>Fails with <paramref name="error"/> when <paramref name="predicate"/> returns false for the value; otherwise passes through unchanged. A no-op on an already-failed result. For a fallible check that itself returns an <see cref="AxisResult"/> (its own errors), use <see cref="Ensure(Func{TValue, AxisResult})"/> instead.</summary>
    public AxisResult<TValue> Ensure(Func<TValue, bool> predicate, AxisError error) => !IsSuccess ? this : (predicate(Value) ? this : Error<TValue>(error));

    /// <inheritdoc cref="Ensure(Func{TValue, bool}, AxisError)"/>
    public async Task<AxisResult<TValue>> EnsureAsync(Func<TValue, Task<bool>> predicate, AxisError error) => !IsSuccess ? this : (await predicate(Value) ? this : Error<TValue>(error));

    /// <summary>Inline business-invariant check: runs <paramref name="validation"/> over the value and propagates ITS errors (rather than a single fixed <see cref="AxisError"/>) when it fails; passes the original value through unchanged when it succeeds. A no-op on an already-failed result.</summary>
    public AxisResult<TValue> Ensure(Func<TValue, AxisResult> validation)
    {
        if (!IsSuccess) return this;
        var validationResult = validation(Value);
        return validationResult.IsSuccess ? this : PropagateErrors<TValue>(validationResult);
    }

    /// <inheritdoc cref="Ensure(Func{TValue, AxisResult})"/>
    public async Task<AxisResult<TValue>> EnsureAsync(Func<TValue, Task<AxisResult>> validation)
    {
        if (!IsSuccess) return this;
        var result = await validation(Value);
        return result.IsSuccess ? this : PropagateErrors<TValue>(result);
    }

    /// <summary>
    /// Success guard: runs the fallible <paramref name="next"/> only when <paramref name="predicate"/>
    /// is FALSE for the value; when the predicate is true, passes through unchanged without running
    /// <paramref name="next"/> at all. The value is preserved either way (same "valueless Then" rule as
    /// <see cref="Then(Func{TValue, AxisResult})"/>). A no-op on an already-failed result.
    /// </summary>
    public AxisResult<TValue> ThenUnless(Func<TValue, bool> predicate, Func<TValue, AxisResult> next)
    {
        if (IsFailure) return this;
        if (predicate(Value)) return this;
        var nextResult = next(Value);
        return nextResult.IsSuccess ? this : PropagateErrors<TValue>(nextResult);
    }

    /// <inheritdoc cref="ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public async Task<AxisResult<TValue>> ThenUnlessAsync(Func<TValue, bool> predicate, Func<TValue, Task<AxisResult>> next)
    {
        if (IsFailure) return this;
        if (predicate(Value)) return this;
        var nextResult = await next(Value);
        return nextResult.IsSuccess ? this : PropagateErrors<TValue>(nextResult);
    }

    /// <summary>
    /// Conditional step: runs the same-type transforming <paramref name="next"/> only when
    /// <paramref name="predicate"/> is TRUE for the value, replacing the result with whatever
    /// <paramref name="next"/> returns; passes through unchanged when the predicate is false. A no-op
    /// on an already-failed result.
    /// </summary>
    public AxisResult<TValue> ThenWhen(Func<TValue, bool> predicate, Func<TValue, AxisResult<TValue>> next)
    {
        if (IsFailure) return this;
        return predicate(Value) ? next(Value) : this;
    }

    /// <inheritdoc cref="ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public async Task<AxisResult<TValue>> ThenWhenAsync(Func<TValue, bool> predicate, Func<TValue, Task<AxisResult<TValue>>> next)
    {
        if (IsFailure) return this;
        return predicate(Value) ? await next(Value) : this;
    }

    /// <summary>Combines the current value with an independently-computed one into a tuple, via a mapper that CANNOT fail. Sequential — unlike the concurrent <c>ZipParallelAsync</c>. Short-circuits on failure.</summary>
    public AxisResult<(TValue Value1, TNew Value2)> Zip<TNew>(Func<TValue, TNew> mapper)
        => IsSuccess ? Ok<(TValue Value1, TNew Value2)>((Value, mapper(Value))) : PropagateErrors<(TValue Value1, TNew Value2)>(this);

    /// <inheritdoc cref="Zip{TNew}(Func{TValue, TNew})"/>
    public async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TNew>(Func<TValue, Task<TNew>> mapper)
        => IsSuccess ? Ok<(TValue Value1, TNew Value2)>((Value, await mapper(Value))) : PropagateErrors<(TValue Value1, TNew Value2)>(this);

    /// <summary>Combines the current value with the value of a fallible <paramref name="mapper"/> into a tuple. Short-circuits (propagating whichever side failed) on either side's failure. Sequential — unlike the concurrent <c>ZipParallelAsync</c>.</summary>
    public AxisResult<(TValue Value1, TNew Value2)> Zip<TNew>(Func<TValue, AxisResult<TNew>> mapper)
    {
        if (!IsSuccess) return PropagateErrors<(TValue Value1, TNew Value2)>(this);
        var r = mapper(Value);
        return r.IsSuccess
            ? Ok<(TValue Value1, TNew Value2)>((Value, r.Value))
            : PropagateErrors<(TValue Value1, TNew Value2)>(r);
    }

    /// <inheritdoc cref="Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TNew>(Func<TValue, Task<AxisResult<TNew>>> mapper)
    {
        if (!IsSuccess) return PropagateErrors<(TValue Value1, TNew Value2)>(this);
        var r = await mapper(Value);
        return r.IsSuccess
            ? Ok<(TValue Value1, TNew Value2)>((Value, r.Value))
            : PropagateErrors<(TValue Value1, TNew Value2)>(r);
    }

    /// <inheritdoc cref="AxisResult.Match{TResult}(Func{TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<IReadOnlyList<AxisError>, TResult> onFailure) => IsSuccess ? onSuccess(Value) : onFailure(Errors);

    /// <inheritdoc cref="AxisResult.Match{TResult}(Func{TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public async Task<TResult> MatchAsync<TResult>(Func<TValue, Task<TResult>> onSuccess, Func<IReadOnlyList<AxisError>, Task<TResult>> onFailure) => IsSuccess ? await onSuccess(Value) : await onFailure(Errors);

    /// <inheritdoc/>
    public override AxisResult<TValue> MapError(Func<IReadOnlyList<AxisError>, IEnumerable<AxisError>> mapper)
        => IsSuccess ? this : Error<TValue>(mapper(Errors));

    /// <inheritdoc cref="AxisResult.MapError(Func{AxisError, AxisError})"/>
    public new AxisResult<TValue> MapError(Func<AxisError, AxisError> mapper)
        => MapError(errors => errors.Select(mapper));

    /// <inheritdoc cref="AxisResult.MapError(Func{IReadOnlyList{AxisError}, IEnumerable{AxisError}})"/>
    public new async Task<AxisResult<TValue>> MapErrorAsync(Func<IReadOnlyList<AxisError>, Task<IEnumerable<AxisError>>> mapper)
        => IsSuccess ? this : Error<TValue>(await mapper(Errors));

    /// <summary>
    /// Deliberately converts ANY failure back to success by producing a value from the error list.
    /// Passes success through unchanged. Recovery must be a conscious narrowing choice — never
    /// blanket-<c>Recover</c> a port call whose infrastructure failures must surface; prefer
    /// <see cref="RecoverWhen(Func{IReadOnlyList{AxisError}, bool}, Func{IReadOnlyList{AxisError}, TValue})"/>,
    /// <see cref="RecoverNotFound(Func{TValue})"/> or <see cref="RecoverConflict(Func{TValue})"/> to
    /// narrow to the specific failures worth recovering from.
    /// </summary>
    public AxisResult<TValue> Recover(Func<IReadOnlyList<AxisError>, TValue> recovery)
        => IsSuccess ? this : Ok(recovery(Errors));

    /// <inheritdoc cref="Recover(Func{IReadOnlyList{AxisError}, TValue})"/>
    public async Task<AxisResult<TValue>> RecoverAsync(Func<IReadOnlyList<AxisError>, Task<TValue>> recovery)
        => IsSuccess ? this : Ok(await recovery(Errors));

    /// <summary>Overload of <see cref="Recover(Func{IReadOnlyList{AxisError}, TValue})"/> whose recovery does not need to inspect the errors.</summary>
    public AxisResult<TValue> Recover(Func<TValue> recovery)
        => IsSuccess ? this : Ok(recovery());

    /// <inheritdoc cref="Recover(Func{TValue})"/>
    public async Task<AxisResult<TValue>> RecoverAsync(Func<Task<TValue>> recovery)
        => IsSuccess ? this : Ok(await recovery());

    /// <summary>Overload of <see cref="Recover(Func{IReadOnlyList{AxisError}, TValue})"/> that recovers with a constant <paramref name="defaultValue"/> on any failure.</summary>
    public AxisResult<TValue> Recover(TValue defaultValue)
        => IsSuccess ? this : Ok(defaultValue);

    /// <summary>Recovers only when <paramref name="predicate"/> returns true for the whole error list; otherwise the failure passes through unchanged. The general, predicate-driven member of the <c>Recover</c> family — see <see cref="RecoverWhen(AxisErrorType, Func{TValue})"/>/<see cref="RecoverWhen(string, Func{TValue})"/> for the type/code-based shortcuts.</summary>
    public AxisResult<TValue> RecoverWhen(Func<IReadOnlyList<AxisError>, bool> predicate, Func<IReadOnlyList<AxisError>, TValue> recovery)
        => IsSuccess ? this : (predicate(Errors) ? Ok(recovery(Errors)) : this);

    /// <inheritdoc cref="RecoverWhen(Func{IReadOnlyList{AxisError}, bool}, Func{IReadOnlyList{AxisError}, TValue})"/>
    public async Task<AxisResult<TValue>> RecoverWhenAsync(Func<IReadOnlyList<AxisError>, bool> predicate, Func<IReadOnlyList<AxisError>, Task<TValue>> recovery)
        => IsSuccess ? this : (predicate(Errors) ? Ok(await recovery(Errors)) : this);

    /// <summary>Recovers when ANY error in the list is of <paramref name="type"/> (<c>Errors.Any</c>, not <c>Errors.All</c> — contrast with <see cref="RecoverNotFound(Func{TValue})"/>/<see cref="RecoverConflict(Func{TValue})"/>, which require every error to match).</summary>
    public AxisResult<TValue> RecoverWhen(AxisErrorType type, Func<TValue> recovery)
        => IsSuccess ? this : (Errors.Any(e => e.Type == type) ? Ok(recovery()) : this);

    /// <inheritdoc cref="RecoverWhen(AxisErrorType, Func{TValue})"/>
    public async Task<AxisResult<TValue>> RecoverWhenAsync(AxisErrorType type, Func<Task<TValue>> recovery)
        => IsSuccess ? this : (Errors.Any(e => e.Type == type) ? Ok(await recovery()) : this);

    /// <summary>Recovers when ANY error in the list has the given <paramref name="code"/> (<c>Errors.Any</c>). See <see cref="RecoverWhen(AxisErrorType, Func{TValue})"/> for the type-based sibling.</summary>
    public AxisResult<TValue> RecoverWhen(string code, Func<TValue> recovery)
        => IsSuccess ? this : (Errors.Any(e => e.Code == code) ? Ok(recovery()) : this);

    /// <inheritdoc cref="RecoverWhen(string, Func{TValue})"/>
    public async Task<AxisResult<TValue>> RecoverWhenAsync(string code, Func<Task<TValue>> recovery)
        => IsSuccess ? this : (Errors.Any(e => e.Code == code) ? Ok(await recovery()) : this);

    /// <summary>
    /// Recovers only when ALL errors are <see cref="AxisErrorType.NotFound"/> (<c>Errors.All</c> — a
    /// mixed failure list is left untouched, unlike <see cref="RecoverWhen(AxisErrorType, Func{TValue})"/>'s
    /// <c>Errors.Any</c>). The get-or-create counterpart of <see cref="AxisResult.RequireNotFound"/>:
    /// where <c>RequireNotFound</c> switches branches, this recovers to a value of the SAME type.
    /// </summary>
    public AxisResult<TValue> RecoverNotFound(Func<TValue> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.NotFound) ? Ok(recovery()) : this);

    /// <inheritdoc cref="RecoverNotFound(Func{TValue})"/>
    public async Task<AxisResult<TValue>> RecoverNotFoundAsync(Func<Task<TValue>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.NotFound) ? Ok(await recovery()) : this);

    /// <summary>Fallible overload of <see cref="RecoverNotFound(Func{TValue})"/>: the recovery itself returns an <see cref="AxisResult{TValue}"/> (e.g. a create call that can also fail) instead of a plain value.</summary>
    public AxisResult<TValue> RecoverNotFound(Func<AxisResult<TValue>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.NotFound) ? recovery() : this);

    /// <inheritdoc cref="RecoverNotFound(Func{AxisResult{TValue}})"/>
    public async Task<AxisResult<TValue>> RecoverNotFoundAsync(Func<Task<AxisResult<TValue>>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.NotFound) ? await recovery() : this);

    /// <summary>
    /// Mirrors <see cref="RecoverNotFound(Func{TValue})"/> for <see cref="AxisErrorType.Conflict"/>
    /// (ALL errors must be Conflict): the loser of a create race recovers by fetching the winner
    /// instead of surfacing the conflict — the write-side idempotency guard.
    /// </summary>
    public AxisResult<TValue> RecoverConflict(Func<TValue> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.Conflict) ? Ok(recovery()) : this);

    /// <inheritdoc cref="RecoverConflict(Func{TValue})"/>
    public async Task<AxisResult<TValue>> RecoverConflictAsync(Func<Task<TValue>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.Conflict) ? Ok(await recovery()) : this);

    /// <summary>Fallible overload of <see cref="RecoverConflict(Func{TValue})"/>: the recovery itself returns an <see cref="AxisResult{TValue}"/> instead of a plain value.</summary>
    public AxisResult<TValue> RecoverConflict(Func<AxisResult<TValue>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.Conflict) ? recovery() : this);

    /// <inheritdoc cref="RecoverConflict(Func{AxisResult{TValue}})"/>
    public async Task<AxisResult<TValue>> RecoverConflictAsync(Func<Task<AxisResult<TValue>>> recovery)
        => IsSuccess ? this : (Errors.All(e => e.Type == AxisErrorType.Conflict) ? await recovery() : this);

    /// <summary>
    /// Converges a found value and a <see cref="AxisErrorType.NotFound"/> miss into a value of a
    /// DIFFERENT type <typeparamref name="TNew"/>: on success, applies <paramref name="map"/> to the
    /// value; on a failure whose errors are ALL <c>NotFound</c>, applies <paramref name="recovery"/>
    /// instead; any other failure propagates unchanged. Not a member of the <c>Recover*</c> family
    /// despite the name — every <c>Recover</c> overload leaves a success untouched, while this one
    /// runs <paramref name="map"/> on the success rail too (required to change the type). Prefer
    /// composing <see cref="Map{TNew}(Func{TValue, TNew})"/> with <see cref="RecoverNotFound(Func{TValue})"/>
    /// instead when the recovered value keeps the SAME type as the source.
    /// </summary>
    public AxisResult<TNew> ElseNotFound<TNew>(Func<TValue, TNew> map, Func<TNew> recovery)
        => IsSuccess ? Ok(map(Value)) : (Errors.All(e => e.Type == AxisErrorType.NotFound) ? Ok(recovery()) : PropagateErrors<TNew>(this));

    /// <inheritdoc cref="ElseNotFound{TNew}(Func{TValue, TNew}, Func{TNew})"/>
    public async Task<AxisResult<TNew>> ElseNotFoundAsync<TNew>(Func<TValue, Task<TNew>> map, Func<Task<TNew>> recovery)
        => IsSuccess ? Ok(await map(Value)) : (Errors.All(e => e.Type == AxisErrorType.NotFound) ? Ok(await recovery()) : PropagateErrors<TNew>(this));

    /// <inheritdoc cref="AxisResult.OrElse(Func{IReadOnlyList{AxisError}, AxisResult})"/>
    public AxisResult<TValue> OrElse(Func<IReadOnlyList<AxisError>, AxisResult<TValue>> fallback)
        => IsSuccess ? this : fallback(Errors);

    /// <inheritdoc cref="AxisResult.OrElse(Func{IReadOnlyList{AxisError}, AxisResult})"/>
    public async Task<AxisResult<TValue>> OrElseAsync(Func<IReadOnlyList<AxisError>, Task<AxisResult<TValue>>> fallback)
        => IsSuccess ? this : await fallback(Errors);

    /// <inheritdoc cref="AxisResult.OrElse(Func{IReadOnlyList{AxisError}, AxisResult}, bool)"/>
    public AxisResult<TValue> OrElse(Func<IReadOnlyList<AxisError>, AxisResult<TValue>> fallback, bool combineErrors)
    {
        if (IsSuccess) return this;
        var alt = fallback(Errors);
        if (alt.IsSuccess) return alt;
        return combineErrors ? Error<TValue>(Errors.Concat(alt.Errors)) : alt;
    }

    /// <inheritdoc cref="AxisResult.OrElse(Func{IReadOnlyList{AxisError}, AxisResult}, bool)"/>
    public async Task<AxisResult<TValue>> OrElseAsync(Func<IReadOnlyList<AxisError>, Task<AxisResult<TValue>>> fallback, bool combineErrors)
    {
        if (IsSuccess) return this;
        var alt = await fallback(Errors);
        if (alt.IsSuccess) return alt;
        return combineErrors ? Error<TValue>(Errors.Concat(alt.Errors)) : alt;
    }

    /// <summary>LINQ query-syntax support: <c>select</c> desugars to this, which simply delegates to <see cref="Map{TNew}(Func{TValue, TNew})"/>.</summary>
    public AxisResult<TNew> Select<TNew>(Func<TValue, TNew> selector) => Map(selector);

    /// <summary>LINQ query-syntax support: enables a <c>from x in r1 from y in binder(x) select projector(x, y)</c> comprehension over two chained <see cref="AxisResult{TValue}"/> sources. Short-circuits (propagating whichever side failed) on either side's failure.</summary>
    public AxisResult<TNew> SelectMany<TIntermediate, TNew>(
        Func<TValue, AxisResult<TIntermediate>> binder,
        Func<TValue, TIntermediate, TNew> projector)
    {
        if (!IsSuccess) return PropagateErrors<TNew>(this);
        var inner = binder(Value);
        return inner.IsSuccess
            ? Ok(projector(Value, inner.Value))
            : PropagateErrors<TNew>(inner);
    }

    /// <inheritdoc cref="SelectMany{TIntermediate, TNew}(Func{TValue, AxisResult{TIntermediate}}, Func{TValue, TIntermediate, TNew})"/>
    public async Task<AxisResult<TNew>> SelectManyAsync<TIntermediate, TNew>(
        Func<TValue, Task<AxisResult<TIntermediate>>> binder,
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
