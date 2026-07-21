using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Handlers;
using System.Data.Common;

namespace AxisRepository.UnitTests;

// Exercises the shared core of AxisRepositoryBase via fakes — no real database — so the dialect-agnostic
// machinery (faulted-transaction guard, transient retry, cancellation propagation, duplicate-key mapping)
// is covered without a provider. The success path + parameter binding are covered by the adapter
// integration tests; here we drive the failure modes that are hard to provoke against a live database.
public class AxisRepositoryBaseTests
{
    private sealed class FakeDbException(bool transient = false, bool duplicate = false, bool schemaMissing = false) : DbException("fake")
    {
        public bool Transient { get; } = transient;
        public bool Duplicate { get; } = duplicate;
        public bool SchemaMissing { get; } = schemaMissing;
    }

    private sealed class FakeDbUnitOfWork : IDbUnitOfWork<DbCommand>
    {
        public bool IsFaulted { get; set; }
        public bool HasUncommittedWrites { get; set; }
        public int MarkFaultedCalls { get; private set; }
        public int ReleaseCalls { get; private set; }
        public Func<string, Task<DbCommand>> OnNewCommand { get; set; } = _ => throw new FakeDbException();

        public Task<DbCommand> NewCommandAsync(string sql) => OnNewCommand(sql);
        public void MarkFaulted() { MarkFaultedCalls++; IsFaulted = true; }
        public void MarkWrite() => HasUncommittedWrites = true;

        public Task<AxisResult> StartAsync() => Task.FromResult(AxisResult.Ok());
        public Task<AxisResult> SaveChangesAsync() => Task.FromResult(AxisResult.Ok());
        public Task<AxisResult> RollbackAsync() => Task.FromResult(AxisResult.Ok());
        public Task ReleaseConnectionAsync() { ReleaseCalls++; return Task.CompletedTask; }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubMediator : IAxisMediator
    {
        public CancellationToken CancellationToken => CancellationToken.None;
        public string TraceId => "t";
        public string OriginId => "o";
        public string? JourneyId => null;
        public AxisEntityId? AxisEntityId => null;
        public IAxisMediatorHandler Cqrs => null!;
    }

    private sealed class FakeRepo(IAxisMediator mediator, IDbUnitOfWork<DbCommand> uow)
        : AxisRepositoryBase<DbCommand, DbDataReader, DbParameterCollection>(mediator, uow)
    {
        protected override bool IsTransient(DbException exception) => exception is FakeDbException { Transient: true };
        protected override bool IsDuplicateKey(DbException exception) => exception is FakeDbException { Duplicate: true };
        protected override bool IsSchemaMissing(DbException exception) => exception is FakeDbException { SchemaMissing: true };
        protected override string ErrorPrefix => "FAKE";
        protected override void LogError(Exception exception, string message) { }
    }

    private static IAxisDbRepository Repo(FakeDbUnitOfWork uow) => new FakeRepo(new StubMediator(), uow);

    [Fact]
    public async Task FaultedTransactionShortCircuitsEverySurface()
    {
        var db = Repo(new FakeDbUnitOfWork { IsFaulted = true });

        await db.ExecuteAsync("x", _ => { }).ShouldFailWithCodeAsync("FAKE_TRANSACTION_FAULTED");
        await db.ExecuteCountAsync("x", _ => { }).ShouldFailWithCodeAsync("FAKE_TRANSACTION_FAULTED");
        await db.GetAsync("x", _ => { }, _ => 1, "NF").ShouldFailWithCodeAsync("FAKE_TRANSACTION_FAULTED");
        await db.ListAsync("x", _ => { }, _ => 1).ShouldFailWithCodeAsync("FAKE_TRANSACTION_FAULTED");
    }

    [Fact]
    public async Task TransientFailureRetriesThenSurfacesTypedTransientError()
    {
        var uow = new FakeDbUnitOfWork { OnNewCommand = _ => throw new FakeDbException(transient: true) };
        var db = Repo(uow);

        var result = await db.ExecuteAsync("x", _ => { });

        // No write landed (HasUncommittedWrites stays false), so each attempt reconnects (ReleaseConnectionAsync)
        // and retries; the four delays are exhausted, then the failure surfaces as a TYPED TRANSIENT — a caller
        // (e.g. the saga engine) can still retry the whole unit of work — and the UoW is faulted.
        var error = Assert.Single(result.Errors);
        Assert.Equal("FAKE_TRANSIENT_ERROR", error.Code);
        Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
        Assert.True(error.IsTransient);
        Assert.Equal(4, uow.ReleaseCalls);
        Assert.Equal(1, uow.MarkFaultedCalls);
    }

    [Fact]
    public async Task TransientAfterAWriteIsSurfacedAsTypedTransientNotRetried()
    {
        var uow = new FakeDbUnitOfWork
        {
            HasUncommittedWrites = true, // a prior write already landed in this transaction
            OnNewCommand = _ => throw new FakeDbException(transient: true),
        };
        var db = Repo(uow);

        var result = await db.ExecuteAsync("x", _ => { });

        // A transient aborts the whole transaction; reconnecting would lose the earlier write, so it is NOT
        // retried in place — the failure surfaces as a TYPED TRANSIENT (faulted, no reconnect) so the caller
        // replays the whole unit of work. This is the ConsumeSeat-under-contention path: transience must survive.
        var error = Assert.Single(result.Errors);
        Assert.Equal("FAKE_TRANSIENT_ERROR", error.Code);
        Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
        Assert.True(error.IsTransient);
        Assert.Equal(0, uow.ReleaseCalls);
        Assert.Equal(1, uow.MarkFaultedCalls);
    }

    [Fact]
    public async Task TransientAfterAWriteSurfacesTypedTransientOnEverySurface()
    {
        FakeDbUnitOfWork AfterWrite() => new()
        {
            HasUncommittedWrites = true,
            OnNewCommand = _ => throw new FakeDbException(transient: true),
        };

        var execute = await Repo(AfterWrite()).ExecuteAsync("x", _ => { });
        var count = await Repo(AfterWrite()).ExecuteCountAsync("x", _ => { });
        var get = await Repo(AfterWrite()).GetAsync("x", _ => { }, _ => 1, "NF");
        var list = await Repo(AfterWrite()).ListAsync("x", _ => { }, _ => 1);

        foreach (var errors in new[] { execute.Errors, count.Errors, get.Errors, list.Errors })
        {
            var error = Assert.Single(errors);
            Assert.Equal("FAKE_TRANSIENT_ERROR", error.Code);
            Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
            Assert.True(error.IsTransient);
        }
    }

    [Fact]
    public async Task OperationCanceledIsRethrownNotMapped()
    {
        var uow = new FakeDbUnitOfWork { OnNewCommand = _ => throw new OperationCanceledException() };
        var db = Repo(uow);

        await Assert.ThrowsAsync<OperationCanceledException>(() => db.ExecuteAsync("x", _ => { }));
    }

    [Fact]
    public async Task DuplicateKeyUsesProvidedCodeOrDialectDefault()
    {
        // A fresh UoW per case: the first duplicate faults the UoW, so reusing it would (correctly) short-circuit.
        FakeDbUnitOfWork Dup() => new() { OnNewCommand = _ => throw new FakeDbException(duplicate: true) };

        var coded = await Repo(Dup()).ExecuteAsync("x", _ => { }, "MY_DUP");
        coded.ShouldFailWithCode("MY_DUP");

        var defaulted = await Repo(Dup()).ExecuteAsync("x", _ => { });
        defaulted.ShouldFailWithCode("FAKE_DUPLICATE_KEY_ERROR");
    }

    [Fact]
    public async Task ExecuteCountDuplicateKeyMapsToConflict()
    {
        var uow = new FakeDbUnitOfWork { OnNewCommand = _ => throw new FakeDbException(duplicate: true) };
        var db = Repo(uow);

        var result = await db.ExecuteCountAsync("x", _ => { });

        result.ShouldFailWithCode("FAKE_DUPLICATE_KEY_ERROR");
    }

    [Fact]
    public async Task ExecuteCountTransientFailureSurfacesTypedTransientError()
    {
        var uow = new FakeDbUnitOfWork { OnNewCommand = _ => throw new FakeDbException(transient: true) };
        var db = Repo(uow);

        var result = await db.ExecuteCountAsync("x", _ => { });

        var error = Assert.Single(result.Errors);
        Assert.Equal("FAKE_TRANSIENT_ERROR", error.Code);
        Assert.True(error.IsTransient);
        Assert.Equal(1, uow.MarkFaultedCalls);
    }

    [Fact]
    public async Task SchemaMissingMapsToTransientSchemaNotReadyOnEverySurface()
    {
        // Pre-migrations state: the relation does not exist yet. Every surface must map it to the
        // typed transient error (ServiceUnavailable) instead of the generic 500 execution error,
        // so pollers can distinguish "waiting for migrations" from a real database failure.
        FakeDbUnitOfWork Missing() => new() { OnNewCommand = _ => throw new FakeDbException(schemaMissing: true) };

        var execute = await Repo(Missing()).ExecuteAsync("x", _ => { });
        var count = await Repo(Missing()).ExecuteCountAsync("x", _ => { });
        var get = await Repo(Missing()).GetAsync("x", _ => { }, _ => 1, "NF");
        var list = await Repo(Missing()).ListAsync("x", _ => { }, _ => 1);

        foreach (var errors in new[] { execute.Errors, count.Errors, get.Errors, list.Errors })
        {
            var error = Assert.Single(errors);
            Assert.Equal("FAKE_SCHEMA_NOT_READY", error.Code);
            Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
            Assert.True(error.IsTransient);
        }
    }

    [Fact]
    public async Task SchemaMissingFaultsTheUnitOfWorkWithoutRetrying()
    {
        var uow = new FakeDbUnitOfWork { OnNewCommand = _ => throw new FakeDbException(schemaMissing: true) };
        var db = Repo(uow);

        var result = await db.ExecuteAsync("x", _ => { });

        // Not a transient in the retry sense: the schema will not appear within a 1.7s in-place
        // retry window, so the failure surfaces immediately (no reconnects) and faults the UoW.
        result.ShouldFail();
        Assert.Equal(0, uow.ReleaseCalls);
        Assert.Equal(1, uow.MarkFaultedCalls);
    }

    [Fact]
    public async Task RealFailureStillMapsToExecutionErrorNotSchemaNotReady()
    {
        // The 42P01-before-migrations vs real-error distinction: a non-schema failure keeps the
        // generic execution error (500), it must never surface as the transient SCHEMA_NOT_READY.
        var uow = new FakeDbUnitOfWork { OnNewCommand = _ => throw new FakeDbException() };
        var db = Repo(uow);

        var result = await db.ExecuteAsync("x", _ => { });

        var error = Assert.Single(result.Errors);
        Assert.Equal("FAKE_EXECUTION_ERROR", error.Code);
        Assert.Equal(AxisErrorType.InternalServerError, error.Type);
        Assert.False(error.IsTransient);
    }

    [Fact]
    public async Task ExecuteCountOperationCanceledIsRethrown()
    {
        var uow = new FakeDbUnitOfWork { OnNewCommand = _ => throw new OperationCanceledException() };
        var db = Repo(uow);

        await Assert.ThrowsAsync<OperationCanceledException>(() => db.ExecuteCountAsync("x", _ => { }));
    }
}
