using System.Diagnostics;

namespace AxisUnitTests;

public class AxisResultZipParallelTests
{
    private static readonly AxisError L1 = AxisError.NotFound("LEFT_NF");
    private static readonly AxisError R1 = AxisError.ValidationRule("RIGHT_VR");

    #region Task

    [Fact]
    public async Task ZipParallelAsync_Task_Both_Success_Returns_Tuple()
    {
        var result = await AxisResult.Ok(1).AsTaskAsync()
            .ZipParallelAsync(() => Task.FromResult(AxisResult.Ok("hello")));

        Assert.True(result.IsSuccess);
        Assert.Equal((1, "hello"), result.Value);
    }

    [Fact]
    public async Task ZipParallelAsync_Task_Actually_Runs_Both_Sides_Concurrently()
    {
        var sw = Stopwatch.StartNew();

        var leftTask = DelayThenOkAsync(150, 1);
        var result = await leftTask.ZipParallelAsync(() => DelayThenOkAsync(150, "x"));

        sw.Stop();
        Assert.True(result.IsSuccess);
        // Sequential would be ~300ms; parallel should be ~150ms. Allow slack for CI.
        Assert.True(sw.ElapsedMilliseconds < 280,
            $"Expected concurrent execution (<280ms), got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ZipParallelAsync_Task_Left_Failure_Propagates()
    {
        var result = await AxisResult.Error<int>(L1).AsTaskAsync()
            .ZipParallelAsync(() => Task.FromResult(AxisResult.Ok("ok")));

        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal(L1, result.Errors[0]);
    }

    [Fact]
    public async Task ZipParallelAsync_Task_Right_Failure_Propagates()
    {
        var result = await AxisResult.Ok(1).AsTaskAsync()
            .ZipParallelAsync(() => Task.FromResult(AxisResult.Error<string>(R1)));

        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal(R1, result.Errors[0]);
    }

    [Fact]
    public async Task ZipParallelAsync_Task_Both_Failures_Are_Accumulated()
    {
        var result = await AxisResult.Error<int>(L1).AsTaskAsync()
            .ZipParallelAsync(() => Task.FromResult(AxisResult.Error<string>(R1)));

        Assert.True(result.IsFailure);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(L1, result.Errors);
        Assert.Contains(R1, result.Errors);
    }

    [Fact]
    public async Task ZipParallelAsync_Task_With_CancellationToken_Forwards_Token()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(1).AsTaskAsync()
            .ZipParallelAsync(ct =>
            {
                observed = ct;
                return Task.FromResult(AxisResult.Ok("x"));
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(cts.Token, observed);
    }

    #endregion

    #region ValueTask

    [Fact]
    public async Task ZipParallelAsync_ValueTask_Both_Success_Returns_Tuple()
    {
        var result = await AxisResult.Ok(1).AsValueTaskAsync()
            .ZipParallelAsync(() => new ValueTask<AxisResult<string>>(AxisResult.Ok("vt")));

        Assert.True(result.IsSuccess);
        Assert.Equal((1, "vt"), result.Value);
    }

    [Fact]
    public async Task ZipParallelAsync_ValueTask_Both_Failures_Are_Accumulated()
    {
        var result = await AxisResult.Error<int>(L1).AsValueTaskAsync()
            .ZipParallelAsync(() => new ValueTask<AxisResult<string>>(AxisResult.Error<string>(R1)));

        Assert.True(result.IsFailure);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(L1, result.Errors);
        Assert.Contains(R1, result.Errors);
    }

    [Fact]
    public async Task ZipParallelAsync_ValueTask_With_CancellationToken_Forwards_Token()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(1).AsValueTaskAsync()
            .ZipParallelAsync(ct =>
            {
                observed = ct;
                return new ValueTask<AxisResult<string>>(AxisResult.Ok("vt"));
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal((1, "vt"), result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ZipParallelAsync_ValueTask_With_CancellationToken_Left_Failure_Propagates()
    {
        var result = await AxisResult.Error<int>(L1).AsValueTaskAsync()
            .ZipParallelAsync(_ =>
                new ValueTask<AxisResult<string>>(AxisResult.Ok("r")), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal(L1, result.Errors[0]);
    }

    [Fact]
    public async Task ZipParallelAsync_ValueTask_With_CancellationToken_Right_Failure_Propagates()
    {
        var result = await AxisResult.Ok(1).AsValueTaskAsync()
            .ZipParallelAsync(_ =>
                new ValueTask<AxisResult<string>>(AxisResult.Error<string>(R1)), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal(R1, result.Errors[0]);
    }

    [Fact]
    public async Task ZipParallelAsync_ValueTask_With_CancellationToken_Both_Failures_Accumulate()
    {
        var result = await AxisResult.Error<int>(L1).AsValueTaskAsync()
            .ZipParallelAsync(_ =>
                new ValueTask<AxisResult<string>>(AxisResult.Error<string>(R1)), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(2, result.Errors.Count);
    }

    #endregion

    private static async Task<AxisResult<T>> DelayThenOkAsync<T>(int ms, T value)
    {
        await Task.Delay(ms);
        return AxisResult.Ok(value);
    }
}
