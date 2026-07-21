namespace Axis;

/// <summary>
/// Extension methods that lift the <see cref="AxisResult"/>/<see cref="AxisResult{TValue}"/> instance
/// operators (documented on those types) onto <see cref="Task{TResult}"/>, so a pipeline can keep
/// chaining <c>...Async</c> calls across <c>await</c> boundaries without an intermediate <c>await</c>
/// per step. Each member here awaits the incoming <see cref="Task{TResult}"/> and delegates to the
/// matching instance method or operator — see the linked member for the operator's actual semantics.
/// </summary>
public static class AxisResultExtensions
{
    /// <summary>Lifts an already-computed <see cref="AxisResult"/> into a completed <see cref="Task{TResult}"/>, to start a fluent <c>...Async</c> chain from a synchronous result.</summary>
    public static Task<AxisResult> AsTaskAsync(this AxisResult axisResult) => Task.FromResult(axisResult);

    /// <inheritdoc cref="AsTaskAsync(AxisResult)"/>
    public static Task<AxisResult<TValue>> AsTaskAsync<TValue>(this AxisResult<TValue> axisResult) => Task.FromResult(axisResult);

    #region Task Extensions (Railway Oriented)

    /// <inheritdoc cref="AxisResult.Then(Func{AxisResult})"/>
    public static async Task<AxisResult> ThenAsync(this Task<AxisResult> task, Func<AxisResult> next)
        => (await task).Then(next);

    /// <summary>
    /// Promotes a valueless <see cref="Task{TResult}">Task&lt;AxisResult&gt;</see> to
    /// <see cref="Task{TResult}">Task&lt;AxisResult&lt;TNew&gt;&gt;</see>, attaching <paramref name="value"/>
    /// on success and propagating the errors unchanged on failure. The canonical use is re-attaching a
    /// value to create after an existence check with <see cref="AxisResult.RequireNotFound"/>, which
    /// lives on the non-generic <see cref="AxisResult"/> and drops the value.
    /// </summary>
    public static async Task<AxisResult<TNew>> WithValueAsync<TNew>(this Task<AxisResult> task, TNew value)
    {
        var result = await task;
        return result.IsSuccess ? AxisResult.Ok(value) : result.Errors.ToArray();
    }

    /// <inheritdoc cref="AxisResult.Then{TNew}(Func{AxisResult{TNew}})"/>
    public static async Task<AxisResult<TNew>> ThenAsync<TNew>(this Task<AxisResult> task, Func<AxisResult<TNew>> next)
        => (await task).Then(next);

    /// <inheritdoc cref="AxisResult.Then(Func{AxisResult})"/>
    public static async Task<AxisResult> ThenAsync(this Task<AxisResult> task, Func<Task<AxisResult>> next)
        => await (await task).ThenAsync(next);

    /// <inheritdoc cref="AxisResult.Then{TNew}(Func{AxisResult{TNew}})"/>
    public static async Task<AxisResult<TNew>> ThenAsync<TNew>(this Task<AxisResult> task, Func<Task<AxisResult<TNew>>> next)
        => await (await task).ThenAsync(next);

    /// <inheritdoc cref="AxisResult.Tap(Action)"/>
    public static async Task<AxisResult> TapAsync(this Task<AxisResult> task, Action action)
        => (await task).Tap(action);

    /// <inheritdoc cref="AxisResult.Tap(Action)"/>
    public static async Task<AxisResult> TapAsync(this Task<AxisResult> task, Func<Task> action)
        => await (await task).TapAsync(action);

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public static async Task<AxisResult> TapErrorAsync(this Task<AxisResult> task, Action<IReadOnlyList<AxisError>> action)
        => (await task).TapError(action);

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public static async Task<AxisResult> TapErrorAsync(this Task<AxisResult> task, Func<IReadOnlyList<AxisError>, Task> action)
        => await (await task).TapErrorAsync(action);

    /// <inheritdoc cref="AxisResult.Match{TResult}(Func{TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public static async Task<TResult> MatchAsync<TResult>(this Task<AxisResult> task, Func<TResult> onSuccess, Func<IReadOnlyList<AxisError>, TResult> onFailure)
        => (await task).Match(onSuccess, onFailure);

    /// <inheritdoc cref="AxisResult.Match{TResult}(Func{TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public static async Task<TResult> MatchAsync<TResult>(this Task<AxisResult> task, Func<Task<TResult>> onSuccess, Func<IReadOnlyList<AxisError>, Task<TResult>> onFailure)
        => await (await task).MatchAsync(onSuccess, onFailure);

    /// <inheritdoc cref="AxisResult.RequireNotFound"/>
    public static async Task<AxisResult> RequireNotFoundAsync(this Task<AxisResult> task, AxisError errorIfFound)
        => (await task).RequireNotFound(errorIfFound);

    // --- Task<AxisResult<TValue>> extensions ---

    /// <inheritdoc cref="AxisResult{TValue}.Map{TNew}(Func{TValue, TNew})"/>
    public static async Task<AxisResult<TNew>> MapAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, TNew> mapper)
        => (await task).Map(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Map{TNew}(Func{TValue, TNew})"/>
    public static async Task<AxisResult<TNew>> MapAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, Task<TNew>> mapper)
        => await (await task).MapAsync(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Then(Func{TValue, AxisResult})"/>
    public static async Task<AxisResult<TValue>> ThenAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, AxisResult> next)
        => (await task).Then(next);

    /// <inheritdoc cref="AxisResult{TValue}.Then(Func{TValue, AxisResult})"/>
    public static async Task<AxisResult<TValue>> ThenAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, Task<AxisResult>> next)
        => await (await task).ThenAsync(next);

    /// <inheritdoc cref="AxisResult{TValue}.Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async Task<AxisResult<TNew>> ThenAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, AxisResult<TNew>> next)
        => (await task).Then(next);

    /// <inheritdoc cref="AxisResult{TValue}.Then{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async Task<AxisResult<TNew>> ThenAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, Task<AxisResult<TNew>>> next)
        => await (await task).ThenAsync(next);

    /// <inheritdoc cref="AxisResult{TValue}.ToAxisResult(Func{TValue, AxisResult})"/>
    public static async Task<AxisResult> ToAxisResultAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, AxisResult> next)
        => (await task).ToAxisResult(next);

    /// <inheritdoc cref="AxisResult{TValue}.ToAxisResult()"/>
    public static async Task<AxisResult> ToAxisResultAsync<TValue>(this Task<AxisResult<TValue>> task)
        => await (await task).ToAxisResultAsync();

    /// <inheritdoc cref="AxisResult{TValue}.ToAxisResult(Func{TValue, AxisResult})"/>
    public static async Task<AxisResult> ToAxisResultAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, Task<AxisResult>> next)
        => await (await task).ToAxisResultAsync(next);

    /// <inheritdoc cref="AxisResult{TValue}.Tap(Action{TValue})"/>
    public static async Task<AxisResult<TValue>> TapAsync<TValue>(this Task<AxisResult<TValue>> task, Action<TValue> action)
        => (await task).Tap(action);

    /// <inheritdoc cref="AxisResult{TValue}.Tap(Action{TValue})"/>
    public static async Task<AxisResult<TValue>> TapAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, Task> action)
        => await (await task).TapAsync(action);

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public static async Task<AxisResult<TValue>> TapErrorAsync<TValue>(this Task<AxisResult<TValue>> task, Action<IReadOnlyList<AxisError>> action)
        => (await task).TapError(action);

    /// <inheritdoc cref="AxisResult.TapError(Action{IReadOnlyList{AxisError}})"/>
    public static async Task<AxisResult<TValue>> TapErrorAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, Task> action)
        => await (await task).TapErrorAsync(action);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, bool}, AxisError)"/>
    public static async Task<AxisResult<TValue>> EnsureAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, bool> predicate, AxisError error)
        => (await task).Ensure(predicate, error);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, bool}, AxisError)"/>
    public static async Task<AxisResult<TValue>> EnsureAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, Task<bool>> predicate, AxisError error)
        => await (await task).EnsureAsync(predicate, error);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, AxisResult})"/>
    public static async Task<AxisResult<TValue>> EnsureAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, AxisResult> validation)
        => (await task).Ensure(validation);

    /// <inheritdoc cref="AxisResult{TValue}.Ensure(Func{TValue, AxisResult})"/>
    public static async Task<AxisResult<TValue>> EnsureAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, Task<AxisResult>> validation)
        => await (await task).EnsureAsync(validation);

    /// <inheritdoc cref="AxisResult{TValue}.ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public static async Task<AxisResult<TValue>> ThenUnlessAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, bool> predicate, Func<TValue, AxisResult> next)
        => (await task).ThenUnless(predicate, next);

    /// <inheritdoc cref="AxisResult{TValue}.ThenUnless(Func{TValue, bool}, Func{TValue, AxisResult})"/>
    public static async Task<AxisResult<TValue>> ThenUnlessAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, bool> predicate, Func<TValue, Task<AxisResult>> next)
        => await (await task).ThenUnlessAsync(predicate, next);

    /// <inheritdoc cref="AxisResult{TValue}.ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public static async Task<AxisResult<TValue>> ThenWhenAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, bool> predicate, Func<TValue, AxisResult<TValue>> next)
        => (await task).ThenWhen(predicate, next);

    /// <inheritdoc cref="AxisResult{TValue}.ThenWhen(Func{TValue, bool}, Func{TValue, AxisResult{TValue}})"/>
    public static async Task<AxisResult<TValue>> ThenWhenAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue, bool> predicate, Func<TValue, Task<AxisResult<TValue>>> next)
        => await (await task).ThenWhenAsync(predicate, next);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, TNew})"/>
    public static async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, TNew> mapper)
        => (await task).Zip(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, TNew})"/>
    public static async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, Task<TNew>> mapper)
        => await (await task).ZipAsync(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, AxisResult<TNew>> mapper)
        => (await task).Zip(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, AxisResult{TNew}})"/>
    public static async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, Task<AxisResult<TNew>>> mapper)
        => await (await task).ZipAsync(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.MapError(Func{IReadOnlyList{AxisError}, IEnumerable{AxisError}})"/>
    public static async Task<AxisResult<TValue>> MapErrorAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, IEnumerable<AxisError>> mapper)
        => (await task).MapError(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.MapError(Func{AxisError, AxisError})"/>
    public static async Task<AxisResult<TValue>> MapErrorAsync<TValue>(this Task<AxisResult<TValue>> task, Func<AxisError, AxisError> mapper)
        => (await task).MapError(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.MapError(Func{IReadOnlyList{AxisError}, IEnumerable{AxisError}})"/>
    public static async Task<AxisResult<TValue>> MapErrorAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, Task<IEnumerable<AxisError>>> mapper)
        => await (await task).MapErrorAsync(mapper);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(Func{IReadOnlyList{AxisError}, TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, TValue> recovery)
        => (await task).Recover(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(Func{IReadOnlyList{AxisError}, TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, Task<TValue>> recovery)
        => await (await task).RecoverAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue> recovery)
        => (await task).Recover(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverAsync<TValue>(this Task<AxisResult<TValue>> task, Func<Task<TValue>> recovery)
        => await (await task).RecoverAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.Recover(TValue)"/>
    public static async Task<AxisResult<TValue>> RecoverAsync<TValue>(this Task<AxisResult<TValue>> task, TValue defaultValue)
        => (await task).Recover(defaultValue);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(Func{IReadOnlyList{AxisError}, bool}, Func{IReadOnlyList{AxisError}, TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverWhenAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, bool> predicate, Func<IReadOnlyList<AxisError>, TValue> recovery)
        => (await task).RecoverWhen(predicate, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(Func{IReadOnlyList{AxisError}, bool}, Func{IReadOnlyList{AxisError}, TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverWhenAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, bool> predicate, Func<IReadOnlyList<AxisError>, Task<TValue>> recovery)
        => await (await task).RecoverWhenAsync(predicate, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(AxisErrorType, Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverWhenAsync<TValue>(this Task<AxisResult<TValue>> task, AxisErrorType type, Func<TValue> recovery)
        => (await task).RecoverWhen(type, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(AxisErrorType, Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverWhenAsync<TValue>(this Task<AxisResult<TValue>> task, AxisErrorType type, Func<Task<TValue>> recovery)
        => await (await task).RecoverWhenAsync(type, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(string, Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverWhenAsync<TValue>(this Task<AxisResult<TValue>> task, string code, Func<TValue> recovery)
        => (await task).RecoverWhen(code, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverWhen(string, Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverWhenAsync<TValue>(this Task<AxisResult<TValue>> task, string code, Func<Task<TValue>> recovery)
        => await (await task).RecoverWhenAsync(code, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverNotFound(Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverNotFoundAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue> recovery)
        => (await task).RecoverNotFound(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverNotFound(Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverNotFoundAsync<TValue>(this Task<AxisResult<TValue>> task, Func<Task<TValue>> recovery)
        => await (await task).RecoverNotFoundAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverNotFound(Func{AxisResult{TValue}})"/>
    public static async Task<AxisResult<TValue>> RecoverNotFoundAsync<TValue>(this Task<AxisResult<TValue>> task, Func<AxisResult<TValue>> recovery)
        => (await task).RecoverNotFound(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverNotFound(Func{AxisResult{TValue}})"/>
    public static async Task<AxisResult<TValue>> RecoverNotFoundAsync<TValue>(this Task<AxisResult<TValue>> task, Func<Task<AxisResult<TValue>>> recovery)
        => await (await task).RecoverNotFoundAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverConflict(Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverConflictAsync<TValue>(this Task<AxisResult<TValue>> task, Func<TValue> recovery)
        => (await task).RecoverConflict(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverConflict(Func{TValue})"/>
    public static async Task<AxisResult<TValue>> RecoverConflictAsync<TValue>(this Task<AxisResult<TValue>> task, Func<Task<TValue>> recovery)
        => await (await task).RecoverConflictAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverConflict(Func{AxisResult{TValue}})"/>
    public static async Task<AxisResult<TValue>> RecoverConflictAsync<TValue>(this Task<AxisResult<TValue>> task, Func<AxisResult<TValue>> recovery)
        => (await task).RecoverConflict(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.RecoverConflict(Func{AxisResult{TValue}})"/>
    public static async Task<AxisResult<TValue>> RecoverConflictAsync<TValue>(this Task<AxisResult<TValue>> task, Func<Task<AxisResult<TValue>>> recovery)
        => await (await task).RecoverConflictAsync(recovery);

    /// <inheritdoc cref="AxisResult{TValue}.ElseNotFound{TNew}(Func{TValue, TNew}, Func{TNew})"/>
    public static async Task<AxisResult<TNew>> ElseNotFoundAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, TNew> map, Func<TNew> recovery)
        => (await task).ElseNotFound(map, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.ElseNotFound{TNew}(Func{TValue, TNew}, Func{TNew})"/>
    public static async Task<AxisResult<TNew>> ElseNotFoundAsync<TValue, TNew>(this Task<AxisResult<TValue>> task, Func<TValue, Task<TNew>> map, Func<Task<TNew>> recovery)
        => await (await task).ElseNotFoundAsync(map, recovery);

    /// <inheritdoc cref="AxisResult{TValue}.OrElse(Func{IReadOnlyList{AxisError}, AxisResult{TValue}})"/>
    public static async Task<AxisResult<TValue>> OrElseAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, AxisResult<TValue>> fallback)
        => (await task).OrElse(fallback);

    /// <inheritdoc cref="AxisResult{TValue}.OrElse(Func{IReadOnlyList{AxisError}, AxisResult{TValue}})"/>
    public static async Task<AxisResult<TValue>> OrElseAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, Task<AxisResult<TValue>>> fallback)
        => await (await task).OrElseAsync(fallback);

    /// <inheritdoc cref="AxisResult{TValue}.OrElse(Func{IReadOnlyList{AxisError}, AxisResult{TValue}}, bool)"/>
    public static async Task<AxisResult<TValue>> OrElseAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, AxisResult<TValue>> fallback, bool combineErrors)
        => (await task).OrElse(fallback, combineErrors);

    /// <inheritdoc cref="AxisResult{TValue}.OrElse(Func{IReadOnlyList{AxisError}, AxisResult{TValue}}, bool)"/>
    public static async Task<AxisResult<TValue>> OrElseAsync<TValue>(this Task<AxisResult<TValue>> task, Func<IReadOnlyList<AxisError>, Task<AxisResult<TValue>>> fallback, bool combineErrors)
        => await (await task).OrElseAsync(fallback, combineErrors);

    /// <inheritdoc cref="AxisResult{TValue}.Match{TResult}(Func{TValue, TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public static async Task<TResult> MatchAsync<TValue, TResult>(this Task<AxisResult<TValue>> task, Func<TValue, TResult> onSuccess, Func<IReadOnlyList<AxisError>, TResult> onFailure)
        => (await task).Match(onSuccess, onFailure);

    /// <inheritdoc cref="AxisResult{TValue}.Match{TResult}(Func{TValue, TResult}, Func{IReadOnlyList{AxisError}, TResult})"/>
    public static async Task<TResult> MatchAsync<TValue, TResult>(this Task<AxisResult<TValue>> task, Func<TValue, Task<TResult>> onSuccess, Func<IReadOnlyList<AxisError>, Task<TResult>> onFailure)
        => await (await task).MatchAsync(onSuccess, onFailure);

    /// <inheritdoc cref="AxisResult{TValue}.SelectMany{TIntermediate, TNew}(Func{TValue, AxisResult{TIntermediate}}, Func{TValue, TIntermediate, TNew})"/>
    public static async Task<AxisResult<TNew>> SelectManyAsync<TValue, TIntermediate, TNew>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, AxisResult<TIntermediate>> binder,
        Func<TValue, TIntermediate, TNew> projector)
        => (await task).SelectMany(binder, projector);

    /// <inheritdoc cref="AxisResult{TValue}.SelectMany{TIntermediate, TNew}(Func{TValue, AxisResult{TIntermediate}}, Func{TValue, TIntermediate, TNew})"/>
    public static async Task<AxisResult<TNew>> SelectManyAsync<TValue, TIntermediate, TNew>(
        this Task<AxisResult<TValue>> task,
        Func<TValue, Task<AxisResult<TIntermediate>>> binder,
        Func<TValue, TIntermediate, TNew> projector)
        => await (await task).SelectManyAsync(binder, projector);

    /// <inheritdoc cref="AxisResult.RequireNotFound"/>
    public static async Task<AxisResult> RequireNotFoundAsync<TValue>(this Task<AxisResult<TValue>> task, AxisError errorIfFound)
        => (await task).RequireNotFound(errorIfFound);

    // --- Task<AxisResult<(T1, T2)>> Tuple2 extensions ---

    /// <summary>
    /// Tuple-spread form of <see cref="AxisResult{TValue}.Map{TNew}(Func{TValue, TNew})"/>: after a
    /// <c>Zip</c>, the tuple's elements are passed as separate delegate parameters instead of one
    /// tuple argument. A step that CANNOT fail. Available for 2-, 3- and 4-element tuples.
    /// </summary>
    public static async Task<AxisResult<TNew>> MapAsync<T1, T2, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, TNew> mapper)
        => (await task).Map(tuple => mapper(tuple.Value1, tuple.Value2));

    /// <inheritdoc cref="MapAsync{T1, T2, TNew}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, TNew})"/>
    public static async Task<AxisResult<TNew>> MapAsync<T1, T2, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, Task<TNew>> mapper)
        => await (await task).MapAsync(tuple => mapper(tuple.Value1, tuple.Value2));

    /// <summary>
    /// Extends a 2-tuple track to a 3-tuple by zipping in one more independently-computed value, via a
    /// mapper that CANNOT fail — the tuple-spread counterpart of <see cref="AxisResult{TValue}.Zip{TNew}(Func{TValue, TNew})"/>.
    /// Short-circuits on failure.
    /// </summary>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ZipAsync<T1, T2, T3>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, T3> mapper)
    {
        var r = await task;
        return r.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3)>((r.Value.Value1, r.Value.Value2, mapper(r.Value.Value1, r.Value.Value2)))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(r.Errors);
    }

    /// <inheritdoc cref="ZipAsync{T1, T2, T3}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, T3})"/>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ZipAsync<T1, T2, T3>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, Task<T3>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(r.Errors);
        var v3 = await mapper(r.Value.Value1, r.Value.Value2);
        return AxisResult.Ok<(T1, T2, T3)>((r.Value.Value1, r.Value.Value2, v3));
    }

    /// <summary>Fallible overload of <see cref="ZipAsync{T1, T2, T3}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, T3})"/>: the zipped-in value comes from an <see cref="AxisResult{TValue}"/> that can itself fail.</summary>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ZipAsync<T1, T2, T3>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, AxisResult<T3>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(r.Errors);
        var m = mapper(r.Value.Value1, r.Value.Value2);
        return m.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3)>((r.Value.Value1, r.Value.Value2, m.Value))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(m.Errors);
    }

    /// <inheritdoc cref="ZipAsync{T1, T2, T3}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, AxisResult{T3}})"/>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ZipAsync<T1, T2, T3>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, Task<AxisResult<T3>>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(r.Errors);
        var m = await mapper(r.Value.Value1, r.Value.Value2);
        return m.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3)>((r.Value.Value1, r.Value.Value2, m.Value))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3)>(m.Errors);
    }

    /// <summary>Tuple-spread valueless form of <see cref="AxisResult{TValue}.Then(Func{TValue, AxisResult})"/>: runs a fallible step over the tuple's elements (spread as separate parameters) and preserves the whole tuple on success.</summary>
    public static async Task<AxisResult<(T1 Value1, T2 Value2)>> ThenAsync<T1, T2>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, AxisResult> next)
        => (await task).Then(tuple => next(tuple.Value1, tuple.Value2));

    /// <inheritdoc cref="ThenAsync{T1, T2}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, AxisResult})"/>
    public static async Task<AxisResult<(T1 Value1, T2 Value2)>> ThenAsync<T1, T2>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, Task<AxisResult>> next)
        => await (await task).ThenAsync(tuple => next(tuple.Value1, tuple.Value2));

    /// <summary>Tuple-spread value-replacing form of <see cref="AxisResult{TValue}.Then{TNew}(Func{TValue, AxisResult{TNew}})"/>: runs a fallible step over the tuple's elements (spread as separate parameters), replacing the tuple with the new value on success.</summary>
    public static async Task<AxisResult<TNew>> ThenAsync<T1, T2, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, AxisResult<TNew>> next)
        => (await task).Then(tuple => next(tuple.Value1, tuple.Value2));

    /// <inheritdoc cref="ThenAsync{T1, T2, TNew}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, AxisResult{TNew}})"/>
    public static async Task<AxisResult<TNew>> ThenAsync<T1, T2, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2)>> task, Func<T1, T2, Task<AxisResult<TNew>>> next)
        => await (await task).ThenAsync(tuple => next(tuple.Value1, tuple.Value2));

    // --- Task<AxisResult<(T1, T2, T3)>> Tuple3 extensions ---

    /// <summary>3-element-tuple arity of <see cref="MapAsync{T1, T2, TNew}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, TNew})"/> — same tuple-spread <c>Map</c> semantics, one more element.</summary>
    public static async Task<AxisResult<TNew>> MapAsync<T1, T2, T3, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, TNew> mapper)
        => (await task).Map(tuple => mapper(tuple.Value1, tuple.Value2, tuple.Value3));

    /// <inheritdoc cref="MapAsync{T1, T2, T3, TNew}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, TNew})"/>
    public static async Task<AxisResult<TNew>> MapAsync<T1, T2, T3, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, Task<TNew>> mapper)
        => await (await task).MapAsync(tuple => mapper(tuple.Value1, tuple.Value2, tuple.Value3));

    /// <summary>4-element-tuple arity of <see cref="ZipAsync{T1, T2, T3}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, T3})"/> — zips in one more independently-computed value, via a mapper that CANNOT fail.</summary>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ZipAsync<T1, T2, T3, T4>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, T4> mapper)
    {
        var r = await task;
        return r.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3, T4)>((r.Value.Value1, r.Value.Value2, r.Value.Value3, mapper(r.Value.Value1, r.Value.Value2, r.Value.Value3)))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(r.Errors);
    }

    /// <inheritdoc cref="ZipAsync{T1, T2, T3, T4}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, T4})"/>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ZipAsync<T1, T2, T3, T4>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, Task<T4>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(r.Errors);
        var v4 = await mapper(r.Value.Value1, r.Value.Value2, r.Value.Value3);
        return AxisResult.Ok<(T1, T2, T3, T4)>((r.Value.Value1, r.Value.Value2, r.Value.Value3, v4));
    }

    /// <summary>Fallible overload of <see cref="ZipAsync{T1, T2, T3, T4}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, T4})"/>: the zipped-in value comes from an <see cref="AxisResult{TValue}"/> that can itself fail.</summary>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ZipAsync<T1, T2, T3, T4>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, AxisResult<T4>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(r.Errors);
        var m = mapper(r.Value.Value1, r.Value.Value2, r.Value.Value3);
        return m.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3, T4)>((r.Value.Value1, r.Value.Value2, r.Value.Value3, m.Value))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(m.Errors);
    }

    /// <inheritdoc cref="ZipAsync{T1, T2, T3, T4}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, AxisResult{T4}})"/>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ZipAsync<T1, T2, T3, T4>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, Task<AxisResult<T4>>> mapper)
    {
        var r = await task;
        if (r.IsFailure) return AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(r.Errors);
        var m = await mapper(r.Value.Value1, r.Value.Value2, r.Value.Value3);
        return m.IsSuccess
            ? AxisResult.Ok<(T1, T2, T3, T4)>((r.Value.Value1, r.Value.Value2, r.Value.Value3, m.Value))
            : AxisResult.Error<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>(m.Errors);
    }

    /// <summary>3-element-tuple arity of <see cref="ThenAsync{T1, T2}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, AxisResult})"/> — same tuple-spread valueless <c>Then</c> semantics, one more element.</summary>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ThenAsync<T1, T2, T3>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, AxisResult> next)
        => (await task).Then(tuple => next(tuple.Value1, tuple.Value2, tuple.Value3));

    /// <inheritdoc cref="ThenAsync{T1, T2, T3}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, AxisResult})"/>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> ThenAsync<T1, T2, T3>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, Task<AxisResult>> next)
        => await (await task).ThenAsync(tuple => next(tuple.Value1, tuple.Value2, tuple.Value3));

    /// <summary>3-element-tuple arity of <see cref="ThenAsync{T1, T2, TNew}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, AxisResult{TNew}})"/> — same tuple-spread value-replacing <c>Then</c> semantics, one more element.</summary>
    public static async Task<AxisResult<TNew>> ThenAsync<T1, T2, T3, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, AxisResult<TNew>> next)
        => (await task).Then(tuple => next(tuple.Value1, tuple.Value2, tuple.Value3));

    /// <inheritdoc cref="ThenAsync{T1, T2, T3, TNew}(Task{AxisResult{ValueTuple{T1, T2, T3}}}, Func{T1, T2, T3, AxisResult{TNew}})"/>
    public static async Task<AxisResult<TNew>> ThenAsync<T1, T2, T3, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3)>> task, Func<T1, T2, T3, Task<AxisResult<TNew>>> next)
        => await (await task).ThenAsync(tuple => next(tuple.Value1, tuple.Value2, tuple.Value3));

    // --- Task<AxisResult<(T1, T2, T3, T4)>> Tuple4 extensions ---

    /// <summary>4-element-tuple arity of <see cref="MapAsync{T1, T2, TNew}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, TNew})"/> — same tuple-spread <c>Map</c> semantics, one more element. Terminal arity: there is no 5-tuple <c>Zip</c>.</summary>
    public static async Task<AxisResult<TNew>> MapAsync<T1, T2, T3, T4, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> task, Func<T1, T2, T3, T4, TNew> mapper)
        => (await task).Map(tuple => mapper(tuple.Value1, tuple.Value2, tuple.Value3, tuple.Value4));

    /// <inheritdoc cref="MapAsync{T1, T2, T3, T4, TNew}(Task{AxisResult{ValueTuple{T1, T2, T3, T4}}}, Func{T1, T2, T3, T4, TNew})"/>
    public static async Task<AxisResult<TNew>> MapAsync<T1, T2, T3, T4, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> task, Func<T1, T2, T3, T4, Task<TNew>> mapper)
        => await (await task).MapAsync(tuple => mapper(tuple.Value1, tuple.Value2, tuple.Value3, tuple.Value4));

    /// <summary>4-element-tuple arity of <see cref="ThenAsync{T1, T2}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, AxisResult})"/> — same tuple-spread valueless <c>Then</c> semantics, one more element.</summary>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ThenAsync<T1, T2, T3, T4>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> task, Func<T1, T2, T3, T4, AxisResult> next)
        => (await task).Then(tuple => next(tuple.Value1, tuple.Value2, tuple.Value3, tuple.Value4));

    /// <inheritdoc cref="ThenAsync{T1, T2, T3, T4}(Task{AxisResult{ValueTuple{T1, T2, T3, T4}}}, Func{T1, T2, T3, T4, AxisResult})"/>
    public static async Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> ThenAsync<T1, T2, T3, T4>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> task, Func<T1, T2, T3, T4, Task<AxisResult>> next)
        => await (await task).ThenAsync(tuple => next(tuple.Value1, tuple.Value2, tuple.Value3, tuple.Value4));

    /// <summary>4-element-tuple arity of <see cref="ThenAsync{T1, T2, TNew}(Task{AxisResult{ValueTuple{T1, T2}}}, Func{T1, T2, AxisResult{TNew}})"/> — same tuple-spread value-replacing <c>Then</c> semantics, one more element.</summary>
    public static async Task<AxisResult<TNew>> ThenAsync<T1, T2, T3, T4, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> task, Func<T1, T2, T3, T4, AxisResult<TNew>> next)
        => (await task).Then(tuple => next(tuple.Value1, tuple.Value2, tuple.Value3, tuple.Value4));

    /// <inheritdoc cref="ThenAsync{T1, T2, T3, T4, TNew}(Task{AxisResult{ValueTuple{T1, T2, T3, T4}}}, Func{T1, T2, T3, T4, AxisResult{TNew}})"/>
    public static async Task<AxisResult<TNew>> ThenAsync<T1, T2, T3, T4, TNew>(this Task<AxisResult<(T1 Value1, T2 Value2, T3 Value3, T4 Value4)>> task, Func<T1, T2, T3, T4, Task<AxisResult<TNew>>> next)
        => await (await task).ThenAsync(tuple => next(tuple.Value1, tuple.Value2, tuple.Value3, tuple.Value4));

    #endregion
}
