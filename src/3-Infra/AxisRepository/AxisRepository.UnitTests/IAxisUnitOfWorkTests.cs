namespace AxisRepository.UnitTests;

public class IAxisUnitOfWorkTests
{
    private sealed class FakeUnitOfWork : IAxisUnitOfWork
    {
        public int StartCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public int RollbackCalls { get; private set; }

        public AxisResult StartResult { get; set; } = AxisResult.Ok();
        public AxisResult SaveResult { get; set; } = AxisResult.Ok();
        public AxisResult RollbackResult { get; set; } = AxisResult.Ok();

        public Task<AxisResult> StartAsync()
        {
            StartCalls++;
            return Task.FromResult(StartResult);
        }

        public Task<AxisResult> SaveChangesAsync()
        {
            SaveCalls++;
            return Task.FromResult(SaveResult);
        }

        public Task<AxisResult> RollbackAsync()
        {
            RollbackCalls++;
            return Task.FromResult(RollbackResult);
        }

        public Task ReleaseConnectionAsync() => Task.CompletedTask;

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // ── InTransactionAsync (non-generic) ───────────────────────────────────

    [Fact]
    public async Task InTransactionAsyncCommitsOnSuccess()
    {
        var uow = new FakeUnitOfWork();

        var result = await ((IAxisUnitOfWork)uow).InTransactionAsync(() => Task.FromResult(AxisResult.Ok()));

        result.ShouldSucceed();
        Assert.Equal(1, uow.StartCalls);
        Assert.Equal(1, uow.SaveCalls);
        Assert.Equal(0, uow.RollbackCalls);
    }

    [Fact]
    public async Task InTransactionAsyncReturnsEarlyWhenStartFails()
    {
        var uow = new FakeUnitOfWork { StartResult = AxisError.BusinessRule("START_FAIL") };

        var result = await ((IAxisUnitOfWork)uow).InTransactionAsync(() => Task.FromResult(AxisResult.Ok()));

        result.ShouldFail();
        Assert.Equal(0, uow.SaveCalls);
        Assert.Equal(0, uow.RollbackCalls);
    }

    [Fact]
    public async Task InTransactionAsyncRollsBackOnFailureResult()
    {
        var uow = new FakeUnitOfWork();

        var result = await ((IAxisUnitOfWork)uow).InTransactionAsync(
            () => Task.FromResult<AxisResult>(AxisError.BusinessRule("WORK_FAIL")));

        result.ShouldFail();
        Assert.Equal(1, uow.SaveCalls == 0 ? uow.RollbackCalls : 0);
        Assert.Equal(1, uow.RollbackCalls);
        Assert.Equal(0, uow.SaveCalls);
    }

    [Fact]
    public async Task InTransactionAsyncRollsBackAndRethrowsOnException()
    {
        var uow = new FakeUnitOfWork();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((IAxisUnitOfWork)uow).InTransactionAsync(() => throw new InvalidOperationException("boom")));

        Assert.Equal(1, uow.RollbackCalls);
        Assert.Equal(0, uow.SaveCalls);
    }

    // ── InTransactionAsync<T> (generic) ────────────────────────────────────

    [Fact]
    public async Task InTransactionAsyncGenericCommitsOnSuccess()
    {
        var uow = new FakeUnitOfWork();

        var result = await ((IAxisUnitOfWork)uow).InTransactionAsync(() => Task.FromResult(AxisResult.Ok(42)));

        result.ShouldSucceedWith(42);
        Assert.Equal(1, uow.SaveCalls);
        Assert.Equal(0, uow.RollbackCalls);
    }

    [Fact]
    public async Task InTransactionAsyncGenericReturnsEarlyWhenStartFails()
    {
        var uow = new FakeUnitOfWork { StartResult = AxisError.BusinessRule("START_FAIL") };

        var result = await ((IAxisUnitOfWork)uow).InTransactionAsync(() => Task.FromResult(AxisResult.Ok(42)));

        result.ShouldFailWithCode("START_FAIL");
        Assert.Equal(0, uow.SaveCalls);
    }

    [Fact]
    public async Task InTransactionAsyncGenericRollsBackOnFailureResult()
    {
        var uow = new FakeUnitOfWork();

        var result = await ((IAxisUnitOfWork)uow).InTransactionAsync(() =>
            Task.FromResult(AxisResult.Error<int>(AxisError.BusinessRule("WORK_FAIL"))));

        result.ShouldFail();
        Assert.Equal(1, uow.RollbackCalls);
        Assert.Equal(0, uow.SaveCalls);
    }

    [Fact]
    public async Task InTransactionAsyncGenericReturnsSaveFailureWhenSaveFails()
    {
        var uow = new FakeUnitOfWork { SaveResult = AxisError.BusinessRule("SAVE_FAIL") };

        var result = await ((IAxisUnitOfWork)uow).InTransactionAsync(() => Task.FromResult(AxisResult.Ok(42)));

        result.ShouldFailWithCode("SAVE_FAIL");
    }

    [Fact]
    public async Task InTransactionAsyncGenericRollsBackAndRethrowsOnException()
    {
        var uow = new FakeUnitOfWork();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((IAxisUnitOfWork)uow).InTransactionAsync<int>(() => throw new InvalidOperationException("boom")));

        Assert.Equal(1, uow.RollbackCalls);
    }
}
