namespace Axis;

public abstract partial class AxisResult
{
    #region Sync

    /// <summary>Creates a successful, valueless <see cref="AxisResult"/>.</summary>
    public static AxisResult Ok() => new AxisResultImpl();

    /// <summary>Creates a successful <see cref="AxisResult{TValue}"/> carrying <paramref name="value"/>.</summary>
    public static AxisResult<TValue> Ok<TValue>(TValue value) => new AxisResultImpl<TValue>(value);

    /// <summary>Creates a failed <see cref="AxisResult"/> carrying a single <see cref="AxisError"/>.</summary>
    public static AxisResult Error(AxisError error) => new AxisResultImpl([error]);

    /// <summary>Creates a failed <see cref="AxisResult"/> carrying every error in <paramref name="errors"/>.</summary>
    public static AxisResult Error(IEnumerable<AxisError> errors) => new AxisResultImpl(errors.ToList());

    /// <summary>Creates a failed <see cref="AxisResult{TValue}"/> carrying a single <see cref="AxisError"/>.</summary>
    public static AxisResult<TValue> Error<TValue>(AxisError error) => new AxisResultImpl<TValue>(default, [error]);

    /// <summary>Creates a failed <see cref="AxisResult{TValue}"/> carrying every error in <paramref name="errors"/>.</summary>
    public static AxisResult<TValue> Error<TValue>(IEnumerable<AxisError> errors) => new AxisResultImpl<TValue>(default, errors.ToList());

    /// <summary>
    /// Aggregates independent valueless results into one: success only when every result in
    /// <paramref name="results"/> succeeds, otherwise a failure carrying the errors of ALL failing
    /// results (not just the first). Use to validate several independent things and report every
    /// problem at once, instead of short-circuiting on the first <c>Then</c>.
    /// </summary>
    public static AxisResult Combine(params AxisResult[] results)
    {
        var errors = results.Where(r => r.IsFailure).SelectMany(r => r.Errors).ToList();
        return errors.Count == 0 ? Ok() : Error(errors);
    }

    /// <inheritdoc cref="Combine(AxisResult[])"/>
    public static AxisResult Combine(IEnumerable<AxisResult> results)
    {
        var errors = results.Where(r => r.IsFailure).SelectMany(r => r.Errors).ToList();
        return errors.Count == 0 ? Ok() : Error(errors);
    }

    /// <summary>
    /// Aggregates independent value-carrying results into one: success with the list of every value
    /// when ALL of <paramref name="results"/> succeed, otherwise a failure carrying the errors of every
    /// failing result (not just the first). The value-carrying counterpart of <see cref="Combine(AxisResult[])"/>.
    /// </summary>
    public static AxisResult<IReadOnlyList<TValue>> All<TValue>(IEnumerable<AxisResult<TValue>> results)
    {
        var resultList = results.ToList();
        var errors = resultList.Where(r => r.IsFailure).SelectMany(r => r.Errors).ToList();
        return errors.Count != 0
            ? Error<IReadOnlyList<TValue>>(errors)
            : Ok<IReadOnlyList<TValue>>(resultList.Select(r => r.Value).ToList());
    }

    internal static AxisResult<TNew> PropagateErrors<TNew>(AxisResult source) => new AxisResultImpl<TNew>(default, source.RawErrors);

    #endregion

    #region Async

    /// <summary>
    /// Awaits every task in <paramref name="tasks"/> concurrently (<see cref="Task.WhenAll(Task[])"/>)
    /// then applies <see cref="All{TValue}(IEnumerable{AxisResult{TValue}})"/> to the outcomes.
    /// </summary>
    public static async Task<AxisResult<IReadOnlyList<TValue>>> AllAsync<TValue>(IEnumerable<Task<AxisResult<TValue>>> tasks)
    {
        var results = await Task.WhenAll(tasks);
        return All(results);
    }

    /// <summary>
    /// Awaits every task in <paramref name="tasks"/> concurrently (<see cref="Task.WhenAll(Task[])"/>)
    /// then applies <see cref="Combine(AxisResult[])"/> to the outcomes.
    /// </summary>
    public static async Task<AxisResult> CombineAsync(IEnumerable<Task<AxisResult>> tasks)
    {
        var results = await Task.WhenAll(tasks);
        return Combine(results);
    }

    /// <inheritdoc cref="AllAsync{TValue}(IEnumerable{Task{AxisResult{TValue}}})"/>
    public static async ValueTask<AxisResult<IReadOnlyList<TValue>>> AllAsync<TValue>(IEnumerable<ValueTask<AxisResult<TValue>>> tasks)
    {
        var list = new List<AxisResult<TValue>>();
        foreach (var t in tasks) list.Add(await t);
        return All(list);
    }

    /// <inheritdoc cref="CombineAsync(IEnumerable{Task{AxisResult}})"/>
    public static async ValueTask<AxisResult> CombineAsync(IEnumerable<ValueTask<AxisResult>> tasks)
    {
        var list = new List<AxisResult>();
        foreach (var t in tasks) list.Add(await t);
        return Combine(list);
    }

    /// <summary>
    /// Sequentially executes <paramref name="operation"/> for each item in <paramref name="items"/>,
    /// then aggregates all results — collecting every error, not just the first.
    /// Each call starts only after the previous one completes.
    /// </summary>
    public static async Task<AxisResult<IReadOnlyList<TResult>>> AllAsync<TSource, TResult>(
        IEnumerable<TSource> items,
        Func<TSource, Task<AxisResult<TResult>>> operation)
    {
        var results = new List<AxisResult<TResult>>();
        foreach (var item in items)
            results.Add(await operation(item));
        return All(results);
    }

    /// <summary>
    /// Sequentially executes <paramref name="operation"/> for each item in <paramref name="items"/>,
    /// then aggregates all results — collecting every error, not just the first.
    /// Each call starts only after the previous one completes.
    /// </summary>
    public static async Task<AxisResult> CombineAsync<TSource>(
        IEnumerable<TSource> items,
        Func<TSource, Task<AxisResult>> operation)
    {
        var results = new List<AxisResult>();
        foreach (var item in items)
            results.Add(await operation(item));
        return Combine(results);
    }

    #endregion

    #region Try

    private static bool IsCritical(Exception ex) => ex is
        OperationCanceledException or
        StackOverflowException or
        OutOfMemoryException or
        ThreadAbortException or
        NullReferenceException or
        ArgumentNullException;

    /// <summary>
    /// Runs <paramref name="action"/> inside a try/catch and converts a thrown exception into a failed
    /// <see cref="AxisResult"/> instead of letting it propagate — the sanctioned exception boundary,
    /// meant for infra/interop code that can only signal failure by throwing. A critical exception
    /// (<see cref="OperationCanceledException"/>, <see cref="OutOfMemoryException"/>,
    /// <see cref="StackOverflowException"/>, <see cref="NullReferenceException"/>,
    /// <see cref="ArgumentNullException"/>, thread abort) is never caught — those signal a programmer
    /// bug or unrecoverable state, not an expected failure.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <param name="errorHandler">Maps the caught exception to an <see cref="AxisError"/>; defaults to <see cref="AxisError.InternalServerError(string)"/> with the exception message.</param>
    public static AxisResult Try(Action action, Func<Exception, AxisError>? errorHandler = null)
    {
        try { action(); return Ok(); }
        catch (Exception ex) when (!IsCritical(ex)) { return Error(errorHandler?.Invoke(ex) ?? AxisError.InternalServerError(ex.Message)); }
    }

    /// <inheritdoc cref="Try(Action, Func{Exception, AxisError})"/>
    public static async Task<AxisResult> TryAsync(Func<Task> action, Func<Exception, AxisError>? errorHandler = null)
    {
        try { await action(); return Ok(); }
        catch (Exception ex) when (!IsCritical(ex)) { return errorHandler?.Invoke(ex) ?? AxisError.InternalServerError(ex.Message); }
    }

    /// <summary>Value-returning form of <see cref="Try(Action, Func{Exception, AxisError})"/>: runs <paramref name="func"/> and wraps its return in <see cref="Ok{TValue}(TValue)"/>, or converts a thrown (non-critical) exception into a failed <see cref="AxisResult{TValue}"/>.</summary>
    /// <param name="func">The function to run.</param>
    /// <param name="errorHandler">Maps the caught exception to an <see cref="AxisError"/>; defaults to <see cref="AxisError.InternalServerError(string)"/> with the exception message.</param>
    public static AxisResult<TValue> Try<TValue>(Func<TValue> func, Func<Exception, AxisError>? errorHandler = null)
    {
        try { return Ok(func()); }
        catch (Exception ex) when (!IsCritical(ex)) { return Error<TValue>(errorHandler?.Invoke(ex) ?? AxisError.InternalServerError(ex.Message)); }
    }

    /// <inheritdoc cref="Try{TValue}(Func{TValue}, Func{Exception, AxisError})"/>
    public static async Task<AxisResult<TValue>> TryAsync<TValue>(Func<Task<TValue>> func, Func<Exception, AxisError>? errorHandler = null)
    {
        try { return Ok(await func()); }
        catch (Exception ex) when (!IsCritical(ex)) { return Error<TValue>(errorHandler?.Invoke(ex) ?? AxisError.InternalServerError(ex.Message)); }
    }

    /// <summary>
    /// Like <see cref="Try(Action, Func{Exception, AxisError})"/>, but <paramref name="func"/> itself
    /// returns an <see cref="AxisResult"/> — use when an operation BOTH throws (network/parse) AND
    /// reports controlled failures (e.g. a non-success HTTP status mapped to an <see cref="AxisError"/>).
    /// The result is flattened, never nested as <c>AxisResult&lt;AxisResult&gt;</c>.
    /// </summary>
    /// <param name="func">The operation to run; may throw or return a failed result.</param>
    /// <param name="errorHandler">Maps a caught (non-critical) exception to an <see cref="AxisError"/>; defaults to <see cref="AxisError.InternalServerError(string)"/> with the exception message.</param>
    /// <remarks>
    /// Task-only by design — there is no <c>ValueTask</c> sibling. A static factory takes its callback
    /// as a lambda, and <c>async () =&gt; ...</c> is ambiguous between <c>Func&lt;Task&gt;</c> and
    /// <c>Func&lt;ValueTask&gt;</c> (would not compile), so adding a <c>ValueTask</c> overload would
    /// break every existing <c>TryBindAsync(async () =&gt; ...)</c> call site. The same reasoning keeps
    /// <see cref="Try(Action, Func{Exception, AxisError})"/>/<see cref="TryAsync(Func{Task}, Func{Exception, AxisError})"/> Task-only.
    /// </remarks>
    public static AxisResult TryBind(Func<AxisResult> func, Func<Exception, AxisError>? errorHandler = null)
    {
        try { return func(); }
        catch (Exception ex) when (!IsCritical(ex)) { return Error(errorHandler?.Invoke(ex) ?? AxisError.InternalServerError(ex.Message)); }
    }

    /// <inheritdoc cref="TryBind(Func{AxisResult}, Func{Exception, AxisError})"/>
    public static AxisResult<TValue> TryBind<TValue>(Func<AxisResult<TValue>> func, Func<Exception, AxisError>? errorHandler = null)
    {
        try { return func(); }
        catch (Exception ex) when (!IsCritical(ex)) { return Error<TValue>(errorHandler?.Invoke(ex) ?? AxisError.InternalServerError(ex.Message)); }
    }

    /// <inheritdoc cref="TryBind(Func{AxisResult}, Func{Exception, AxisError})"/>
    public static async Task<AxisResult> TryBindAsync(Func<Task<AxisResult>> func, Func<Exception, AxisError>? errorHandler = null)
    {
        try { return await func(); }
        catch (Exception ex) when (!IsCritical(ex)) { return errorHandler?.Invoke(ex) ?? AxisError.InternalServerError(ex.Message); }
    }

    /// <inheritdoc cref="TryBind(Func{AxisResult}, Func{Exception, AxisError})"/>
    public static async Task<AxisResult<TValue>> TryBindAsync<TValue>(Func<Task<AxisResult<TValue>>> func, Func<Exception, AxisError>? errorHandler = null)
    {
        try { return await func(); }
        catch (Exception ex) when (!IsCritical(ex)) { return Error<TValue>(errorHandler?.Invoke(ex) ?? AxisError.InternalServerError(ex.Message)); }
    }

    #endregion
}
