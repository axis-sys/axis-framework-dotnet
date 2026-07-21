namespace Axis;

/// <summary>
/// <see cref="ValueTask{TResult}"/> counterpart of <see cref="AxisResultExtensions"/>: each member awaits
/// the incoming <see cref="ValueTask{TResult}"/> and delegates to the matching instance operator on
/// <see cref="AxisResult"/>/<see cref="AxisResult{TValue}"/> — see the linked member for the operator's
/// actual semantics.
/// </summary>
public static class AxisResultValueTaskExtensions
{
    /// <inheritdoc cref="AxisResultExtensions.AsTaskAsync(AxisResult)"/>
    public static ValueTask<AxisResult> AsValueTaskAsync(this AxisResult axisResult) => new(axisResult);

    /// <inheritdoc cref="AxisResultExtensions.AsTaskAsync(AxisResult)"/>
    public static ValueTask<AxisResult<TValue>> AsValueTaskAsync<TValue>(this AxisResult<TValue> axisResult) => new(axisResult);

    #region ValueTask Extensions (Railway Oriented)

    // --- ValueTask<AxisResult> extensions ---

    /// <inheritdoc cref="AxisResult.Then(Func{AxisResult})"/>
    public static async ValueTask<AxisResult> ThenAsync(this ValueTask<AxisResult> task, Func<AxisResult> next)
        => (await task).Then(next);

    /// <inheritdoc cref="AxisResult.Then{TNew}(Func{AxisResult{TNew}})"/>
    public static async ValueTask<AxisResult<TNew>> ThenAsync<TNew>(this ValueTask<AxisResult> task, Func<AxisResult<TNew>> next)
        => (await task).Then(next);

    /// <inheritdoc cref="AxisResult.Then(Func{AxisResult})"/>
    public static async ValueTask<AxisResult> ThenAsync(this ValueTask<AxisResult> task, Func<ValueTask<AxisResult>> next)
        => await (await task).ThenAsync(next);

    /// <inheritdoc cref="AxisResult.Then{TNew}(Func{AxisResult{TNew}})"/>
    public static async ValueTask<AxisResult<TNew>> ThenAsync<TNew>(this ValueTask<AxisResult> task, Func<ValueTask<AxisResult<TNew>>> next)
        => await (await task).ThenAsync(next);

    /// <inheritdoc cref="AxisResult.Tap(Action)"/>
    public static async ValueTask<AxisResult> TapAsync(this ValueTask<AxisResult> task, Action action)
        => (await task).Tap(action);

    /// <inheritdoc cref="AxisResult.Tap(Action)"/>
    public static async ValueTask<AxisResult> TapAsync(this ValueTask<AxisResult> task, Func<ValueTask> action)
        => await (await task).TapAsync(action);

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public static async ValueTask<AxisResult> TapErrorAsync(this ValueTask<AxisResult> task, Action<IReadOnlyList<AxisError>> action)
        => (await task).TapError(action);

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public static async ValueTask<AxisResult> TapErrorAsync(this ValueTask<AxisResult> task, Func<IReadOnlyList<AxisError>, ValueTask> action)
        => await (await task).TapErrorAsync(action);

    /// <inheritdoc cref="AxisResult.Match{TResult}(Func{TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public static async ValueTask<TResult> MatchAsync<TResult>(this ValueTask<AxisResult> task, Func<TResult> onSuccess, Func<IReadOnlyList<AxisError>, TResult> onFailure)
        => (await task).Match(onSuccess, onFailure);

    /// <inheritdoc cref="AxisResult.Match{TResult}(Func{TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public static async ValueTask<TResult> MatchAsync<TResult>(this ValueTask<AxisResult> task, Func<ValueTask<TResult>> onSuccess, Func<IReadOnlyList<AxisError>, ValueTask<TResult>> onFailure)
        => await (await task).MatchAsync(onSuccess, onFailure);

    /// <inheritdoc cref="AxisResultExtensions.WithValueAsync{TNew}(Task{AxisResult}, TNew)"/>
    public static async ValueTask<AxisResult<TNew>> WithValueAsync<TNew>(this ValueTask<AxisResult> task, TNew value)
    {
        var result = await task;
        return result.IsSuccess ? AxisResult.Ok(value) : AxisResult.Error<TNew>(result.Errors);
    }

    /// <inheritdoc cref="AxisResult.RequireNotFound"/>
    public static async ValueTask<AxisResult> RequireNotFoundAsync(this ValueTask<AxisResult> task, AxisError errorIfFound)
        => (await task).RequireNotFound(errorIfFound);

    // --- ValueTask<AxisResult<TValue>> extensions ---

    /// <inheritdoc cref="AxisResult{TValue}.Map{TNew}(Func{TValue, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> MapAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, TNew> mapper)
        => (await task).Map(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Map{TNew}(Func{TValue, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> MapAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<TNew>> mapper)
        => await (await task).MapAsync(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Then(Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> ActionAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<AxisResult>> next)
        => await (await task).ThenAsync(next);

    /// <inheritdoc cref="AxisResult{TValue}.Then(Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> ThenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, AxisResult> next)
        => (await task).Then(next);

    /// <inheritdoc cref="AxisResult{TValue}.Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async ValueTask<AxisResult<TNew>> ThenAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, AxisResult<TNew>> next)
        => (await task).Then(next);

    /// <inheritdoc cref="AxisResult{TValue}.Then(Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> ThenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<AxisResult>> next)
        => await (await task).ThenAsync(next);

    /// <inheritdoc cref="AxisResult{TValue}.Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async ValueTask<AxisResult<TNew>> ThenAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<AxisResult<TNew>>> next)
        => await (await task).ThenAsync(next);

    /// <inheritdoc cref="AxisResult{TValue}.Tap(Action{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> TapAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Action<TValue> action)
        => (await task).Tap(action);

    /// <inheritdoc cref="AxisResult{TValue}.Tap(Action{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> TapAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask> action)
        => await (await task).TapAsync(action);

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public static async ValueTask<AxisResult<TValue>> TapErrorAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Action<IReadOnlyList<AxisError>> action)
        => (await task).TapError(action);

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public static async ValueTask<AxisResult<TValue>> TapErrorAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, ValueTask> action)
        => await (await task).TapErrorAsync(action);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, bool}, AxisError)"/>
    public static async ValueTask<AxisResult<TValue>> EnsureAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, bool> predicate, AxisError error)
        => (await task).Ensure(predicate, error);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, bool}, AxisError)"/>
    public static async ValueTask<AxisResult<TValue>> EnsureAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<bool>> predicate, AxisError error)
        => await (await task).EnsureAsync(predicate, error);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> EnsureAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, AxisResult> validation)
        => (await task).Ensure(validation);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> EnsureAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<AxisResult>> validation)
        => await (await task).EnsureAsync(validation);

    /// <inheritdoc cref="AxisResult{TValue}.ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> ThenUnlessAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, bool> predicate, Func<TValue, AxisResult> next)
        => (await task).ThenUnless(predicate, next);

    /// <inheritdoc cref="AxisResult{TValue}.ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public static async ValueTask<AxisResult<TValue>> ThenUnlessAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, bool> predicate, Func<TValue, ValueTask<AxisResult>> next)
        => await (await task).ThenUnlessAsync(predicate, next);

    /// <inheritdoc cref="AxisResult{TValue}.ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public static async ValueTask<AxisResult<TValue>> ThenWhenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, bool> predicate, Func<TValue, AxisResult<TValue>> next)
        => (await task).ThenWhen(predicate, next);

    /// <inheritdoc cref="AxisResult{TValue}.ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public static async ValueTask<AxisResult<TValue>> ThenWhenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue, bool> predicate, Func<TValue, ValueTask<AxisResult<TValue>>> next)
        => await (await task).ThenWhenAsync(predicate, next);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, TNew})"/>
    public static async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, TNew> mapper)
        => (await task).Zip(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, TNew})"/>
    public static async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<TNew>> mapper)
        => await (await task).ZipAsync(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, AxisResult<TNew>> mapper)
        => (await task).Zip(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<AxisResult<TNew>>> mapper)
        => await (await task).ZipAsync(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.MapError(Func{IReadOnlyList{AxisError}, IEnumerable{AxisError}})"/>
    public static async ValueTask<AxisResult<TValue>> MapErrorAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, IEnumerable<AxisError>> mapper)
        => (await task).MapError(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.MapError(Func{AxisError, AxisError})"/>
    public static async ValueTask<AxisResult<TValue>> MapErrorAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<AxisError, AxisError> mapper)
        => (await task).MapError(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.MapError(Func{IReadOnlyList{AxisError}, IEnumerable{AxisError}})"/>
    public static async ValueTask<AxisResult<TValue>> MapErrorAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, ValueTask<IEnumerable<AxisError>>> mapper)
        => await (await task).MapErrorAsync(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(Func{IReadOnlyList{AxisError}, TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, TValue> recovery)
        => (await task).Recover(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(Func{IReadOnlyList{AxisError}, TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, ValueTask<TValue>> recovery)
        => await (await task).RecoverAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue> recovery)
        => (await task).Recover(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<ValueTask<TValue>> recovery)
        => await (await task).RecoverAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(TValue)"/>
    public static async ValueTask<AxisResult<TValue>> RecoverAsync<TValue>(this ValueTask<AxisResult<TValue>> task, TValue defaultValue)
        => (await task).Recover(defaultValue);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(Func{IReadOnlyList{AxisError}, bool}, Func{IReadOnlyList{AxisError}, TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverWhenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, bool> predicate, Func<IReadOnlyList<AxisError>, TValue> recovery)
        => (await task).RecoverWhen(predicate, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(Func{IReadOnlyList{AxisError}, bool}, Func{IReadOnlyList{AxisError}, TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverWhenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, bool> predicate, Func<IReadOnlyList<AxisError>, ValueTask<TValue>> recovery)
        => await (await task).RecoverWhenAsync(predicate, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(AxisErrorType, Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverWhenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, AxisErrorType type, Func<TValue> recovery)
        => (await task).RecoverWhen(type, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(AxisErrorType, Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverWhenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, AxisErrorType type, Func<ValueTask<TValue>> recovery)
        => await (await task).RecoverWhenAsync(type, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(string, Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverWhenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, string code, Func<TValue> recovery)
        => (await task).RecoverWhen(code, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(string, Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverWhenAsync<TValue>(this ValueTask<AxisResult<TValue>> task, string code, Func<ValueTask<TValue>> recovery)
        => await (await task).RecoverWhenAsync(code, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverNotFound(Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverNotFoundAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue> recovery)
        => (await task).RecoverNotFound(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverNotFound(Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverNotFoundAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<ValueTask<TValue>> recovery)
        => await (await task).RecoverNotFoundAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverNotFound(Func{AxisResult{TValue}})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverNotFoundAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<AxisResult<TValue>> recovery)
        => (await task).RecoverNotFound(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverNotFound(Func{AxisResult{TValue}})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverNotFoundAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<ValueTask<AxisResult<TValue>>> recovery)
        => await (await task).RecoverNotFoundAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverConflict(Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverConflictAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<TValue> recovery)
        => (await task).RecoverConflict(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverConflict(Func{TValue})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverConflictAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<ValueTask<TValue>> recovery)
        => await (await task).RecoverConflictAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverConflict(Func{AxisResult{TValue}})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverConflictAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<AxisResult<TValue>> recovery)
        => (await task).RecoverConflict(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverConflict(Func{AxisResult{TValue}})"/>
    public static async ValueTask<AxisResult<TValue>> RecoverConflictAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<ValueTask<AxisResult<TValue>>> recovery)
        => await (await task).RecoverConflictAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.ElseNotFound{TNew}(Func{TValue, TNew}, Func{TNew})"/>
    public static async ValueTask<AxisResult<TNew>> ElseNotFoundAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, TNew> map, Func<TNew> recovery)
        => (await task).ElseNotFound(map, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.ElseNotFound{TNew}(Func{TValue, TNew}, Func{TNew})"/>
    public static async ValueTask<AxisResult<TNew>> ElseNotFoundAsync<TValue, TNew>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<TNew>> map, Func<ValueTask<TNew>> recovery)
        => await (await task).ElseNotFoundAsync(map, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.OrElse(Func{IReadOnlyList{AxisError}, AxisResult{TValue}})"/>
    public static async ValueTask<AxisResult<TValue>> OrElseAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, AxisResult<TValue>> fallback)
        => (await task).OrElse(fallback);

    /// <inheritdoc cref="AxisResult{TValue}.OrElse(Func{IReadOnlyList{AxisError}, AxisResult{TValue}})"/>
    public static async ValueTask<AxisResult<TValue>> OrElseAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, ValueTask<AxisResult<TValue>>> fallback)
        => await (await task).OrElseAsync(fallback);

    /// <inheritdoc cref="AxisResult{TValue}.OrElse(Func{IReadOnlyList{AxisError}, AxisResult{TValue}}, bool)"/>
    public static async ValueTask<AxisResult<TValue>> OrElseAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, AxisResult<TValue>> fallback, bool combineErrors)
        => (await task).OrElse(fallback, combineErrors);

    /// <inheritdoc cref="AxisResult{TValue}.OrElse(Func{IReadOnlyList{AxisError}, AxisResult{TValue}}, bool)"/>
    public static async ValueTask<AxisResult<TValue>> OrElseAsync<TValue>(this ValueTask<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, ValueTask<AxisResult<TValue>>> fallback, bool combineErrors)
        => await (await task).OrElseAsync(fallback, combineErrors);

    /// <inheritdoc cref="AxisResult{TValue}.Match{TResult}(Func{TValue, TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public static async ValueTask<TResult> MatchAsync<TValue, TResult>(this ValueTask<AxisResult<TValue>> task, Func<TValue, TResult> onSuccess, Func<IReadOnlyList<AxisError>, TResult> onFailure)
        => (await task).Match(onSuccess, onFailure);

    /// <inheritdoc cref="AxisResult{TValue}.Match{TResult}(Func{TValue, TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public static async ValueTask<TResult> MatchAsync<TValue, TResult>(this ValueTask<AxisResult<TValue>> task, Func<TValue, ValueTask<TResult>> onSuccess, Func<IReadOnlyList<AxisError>, ValueTask<TResult>> onFailure)
        => await (await task).MatchAsync(onSuccess, onFailure);

    /// <inheritdoc cref="AxisResult{TValue}.SelectMany{TIntermediate, TNew}(Func{TValue, AxisResult{TIntermediate}}, Func{TValue, TIntermediate, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> SelectManyAsync<TValue, TIntermediate, TNew>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, AxisResult<TIntermediate>> binder,
        Func<TValue, TIntermediate, TNew> projector)
        => (await task).SelectMany(binder, projector);

    /// <inheritdoc cref="AxisResult{TValue}.SelectMany{TIntermediate, TNew}(Func{TValue, AxisResult{TIntermediate}}, Func{TValue, TIntermediate, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> SelectManyAsync<TValue, TIntermediate, TNew>(
        this ValueTask<AxisResult<TValue>> task,
        Func<TValue, ValueTask<AxisResult<TIntermediate>>> binder,
        Func<TValue, TIntermediate, TNew> projector)
        => await (await task).SelectManyAsync(binder, projector);

    /// <inheritdoc cref="AxisResult.RequireNotFound"/>
    public static async ValueTask<AxisResult> RequireNotFoundAsync<TValue>(this ValueTask<AxisResult<TValue>> task, AxisError errorIfFound)
        => (await task).RequireNotFound(errorIfFound);

    // --- ValueTask<AxisResult<(T1, T2)>> Tuple2 extensions ---

    /// <inheritdoc cref="AxisResultExtensions.MapAsync{T1, T2, TNew}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> MapAsync<T1, T2, TNew>(this ValueTask<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, TNew> mapper)
        => (await task).Map(tuple => mapper(tuple.Value1, tuple.Value2));

    /// <inheritdoc cref="AxisResultExtensions.MapAsync{T1, T2, TNew}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> MapAsync<T1, T2, TNew>(this ValueTask<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, ValueTask<TNew>> mapper)
        => await (await task).MapAsync(tuple => mapper(tuple.Value1, tuple.Value2));

    /// <inheritdoc cref="AxisResultExtensions.ZipAsync{T1, T2, T3}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, T3})"/>
    public static async ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ZipAsync<T1, T2, T3>(this ValueTask<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, T3> mapper)
    {
        var r = await task;
        return r.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3)>((r.Value.Value1, r.Value.Value2, mapper(r.Value.Value1, r.Value.Value2)))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(r.Errors);
    }

    /// <inheritdoc cref="AxisResultExtensions.ZipAsync{T1, T2, T3}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, T3})"/>
    public static async ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ZipAsync<T1, T2, T3>(this ValueTask<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, ValueTask<T3>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(r.Errors);
        var v3 = await mapper(r.Value.Value1, r.Value.Value2);
        return AxisResult.Ok<(T1, T2, T3)>((r.Value.Value1, r.Value.Value2, v3));
    }

    /// <inheritdoc cref="AxisResultExtensions.ZipAsync{T1, T2, T3}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, AxisResult{T3}})"/>
    public static async ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ZipAsync<T1, T2, T3>(this ValueTask<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, AxisResult<T3>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(r.Errors);
        var m = mapper(r.Value.Value1, r.Value.Value2);
        return m.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3)>((r.Value.Value1, r.Value.Value2, m.Value))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(m.Errors);
    }

    /// <inheritdoc cref="AxisResultExtensions.ZipAsync{T1, T2, T3}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, AxisResult{T3}})"/>
    public static async ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ZipAsync<T1, T2, T3>(this ValueTask<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, ValueTask<AxisResult<T3>>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(r.Errors);
        var m = await mapper(r.Value.Value1, r.Value.Value2);
        return m.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3)>((r.Value.Value1, r.Value.Value2, m.Value))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(m.Errors);
    }

    // --- ValueTask<AxisResult<(T1, T2, T3)>> Tuple3 extensions ---

    /// <inheritdoc cref="AxisResultExtensions.MapAsync{T1, T2, T3, TNew}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> MapAsync<T1, T2, T3, TNew>(this ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, TNew> mapper)
        => (await task).Map(tuple => mapper(tuple.Value1, tuple.Value2, tuple.Value3));

    /// <inheritdoc cref="AxisResultExtensions.MapAsync{T1, T2, T3, TNew}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> MapAsync<T1, T2, T3, TNew>(this ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, ValueTask<TNew>> mapper)
        => await (await task).MapAsync(tuple => mapper(tuple.Value1, tuple.Value2, tuple.Value3));

    /// <inheritdoc cref="AxisResultExtensions.ZipAsync{T1, T2, T3, T4}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, T4})"/>
    public static async ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ZipAsync<T1, T2, T3, T4>(this ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, T4> mapper)
    {
        var r = await task;
        return r.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3, T4)>((r.Value.Value1, r.Value.Value2, r.Value.Value3, mapper(r.Value.Value1, r.Value.Value2, r.Value.Value3)))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(r.Errors);
    }

    /// <inheritdoc cref="AxisResultExtensions.ZipAsync{T1, T2, T3, T4}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, T4})"/>
    public static async ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ZipAsync<T1, T2, T3, T4>(this ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, ValueTask<T4>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(r.Errors);
        var v4 = await mapper(r.Value.Value1, r.Value.Value2, r.Value.Value3);
        return AxisResult.Ok<(T1, T2, T3, T4)>((r.Value.Value1, r.Value.Value2, r.Value.Value3, v4));
    }

    /// <inheritdoc cref="AxisResultExtensions.ZipAsync{T1, T2, T3, T4}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, AxisResult{T4}})"/>
    public static async ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ZipAsync<T1, T2, T3, T4>(this ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, AxisResult<T4>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(r.Errors);
        var m = mapper(r.Value.Value1, r.Value.Value2, r.Value.Value3);
        return m.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3, T4)>((r.Value.Value1, r.Value.Value2, r.Value.Value3, m.Value))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(m.Errors);
    }

    /// <inheritdoc cref="AxisResultExtensions.ZipAsync{T1, T2, T3, T4}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, AxisResult{T4}})"/>
    public static async ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ZipAsync<T1, T2, T3, T4>(this ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, ValueTask<AxisResult<T4>>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(r.Errors);
        var m = await mapper(r.Value.Value1, r.Value.Value2, r.Value.Value3);
        return m.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3, T4)>((r.Value.Value1, r.Value.Value2, r.Value.Value3, m.Value))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(m.Errors);
    }

    // --- ValueTask<AxisResult<(T1, T2, T3, T4)>> Tuple4 extensions ---

    /// <inheritdoc cref="AxisResultExtensions.MapAsync{T1, T2, T3, T4, TNew}(Task{AxisResult{ValueTuple{T1, T2, T3, T4}}}, Func{T1, T2, T3, T4, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> MapAsync<T1, T2, T3, T4, TNew>(this ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> task, Func<T1, T2, T3, T4, TNew> mapper)
        => (await task).Map(tuple => mapper(tuple.Value1, tuple.Value2, tuple.Value3, tuple.Value4));

    /// <inheritdoc cref="AxisResultExtensions.MapAsync{T1, T2, T3, T4, TNew}(Task{AxisResult{ValueTuple{T1, T2, T3, T4}}}, Func{T1, T2, T3, T4, TNew})"/>
    public static async ValueTask<AxisResult<TNew>> MapAsync<T1, T2, T3, T4, TNew>(this ValueTask<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> task, Func<T1, T2, T3, T4, ValueTask<TNew>> mapper)
        => await (await task).MapAsync(tuple => mapper(tuple.Value1, tuple.Value2, tuple.Value3, tuple.Value4));

    #endregion
}
