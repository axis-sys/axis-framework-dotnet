namespace Axis;

// Parallel composition: run an independent operation concurrently with the
// upstream pipeline and zip the results into a tuple. Unlike the regular
// ZipAsync (which is fail-fast and sequential), this runs both sides
// concurrently and accumulates errors if BOTH sides fail — otherwise it
// propagates whichever side failed.
public static class AxisResultParallelExtensions
{
    #region Task: ZipParallelAsync

    /// <summary>
    /// Runs <paramref name="other"/> concurrently with the upstream <paramref name="task"/>.
    /// Both sides are independent (the factory receives no TValue), so they start in
    /// parallel. Fails if either side fails; accumulates both error lists if both fail.
    /// </summary>
    public static async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipParallelAsync<TValue, TNew>(
        this Task<AxisResult<TValue>> task,
        Func<Task<AxisResult<TNew>>> other)
    {
        var otherTask = other();
        await Task.WhenAll(task, otherTask);
        var r1 = await task;
        var r2 = await otherTask;
        return Combine(r1, r2);
    }

    public static async Task<AxisResult<(TValue Value1, TNew Value2)>> ZipParallelAsync<TValue, TNew>(
        this Task<AxisResult<TValue>> task,
        Func<CancellationToken, Task<AxisResult<TNew>>> other,
        CancellationToken ct = default)
    {
        var otherTask = other(ct);
        await Task.WhenAll(task, otherTask);
        var r1 = await task;
        var r2 = await otherTask;
        return Combine(r1, r2);
    }

    #endregion

    #region ValueTask: ZipParallelAsync

    /// <summary>
    /// ValueTask variant of <see cref="ZipParallelAsync{TValue,TNew}(Task{AxisResult{TValue}}, Func{Task{AxisResult{TNew}}})"/>.
    /// Both ValueTasks are materialized into Tasks to enable <see cref="Task.WhenAll(Task[])"/> —
    /// parallelism is only meaningful when at least one side is genuinely asynchronous.
    /// </summary>
    public static async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipParallelAsync<TValue, TNew>(
        this ValueTask<AxisResult<TValue>> task,
        Func<ValueTask<AxisResult<TNew>>> other)
    {
        var leftTask = task.AsTask();
        var rightTask = other().AsTask();
        await Task.WhenAll(leftTask, rightTask);
        return Combine(await leftTask, await rightTask);
    }

    public static async ValueTask<AxisResult<(TValue Value1, TNew Value2)>> ZipParallelAsync<TValue, TNew>(
        this ValueTask<AxisResult<TValue>> task,
        Func<CancellationToken, ValueTask<AxisResult<TNew>>> other,
        CancellationToken ct = default)
    {
        var leftTask = task.AsTask();
        var rightTask = other(ct).AsTask();
        await Task.WhenAll(leftTask, rightTask);
        return Combine(await leftTask, await rightTask);
    }

    #endregion

    private static AxisResult<(TValue Value1, TNew Value2)> Combine<TValue, TNew>(
        AxisResult<TValue> left,
        AxisResult<TNew> right)
    {
        return left.IsFailure switch
        {
            true when right.IsFailure => AxisResult.Error<(TValue Value1, TNew Value2)>(left.Errors.Concat(right.Errors)),
            true => AxisResult.Error<(TValue Value1, TNew Value2)>(left.Errors),
            _ => right.IsFailure
                ? AxisResult.Error<(TValue Value1, TNew Value2)>(right.Errors)
                : AxisResult.Ok<(TValue Value1, TNew Value2)>((left.Value, right.Value))
        };
    }
}
