using Axis.Contracts;
using Axis.Contracts.Configuration;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.Postgres.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisSaga.Postgres.IntegrationTests;

[Collection("AxisSagaPostgresCollection")]
public class PostgresSagaMediatorTests(AxisSagaPostgresFixture fixture)
{
    public record TestPayload
    {
        public string? Result { get; init; }
    }

    private (IServiceProvider sp, IAxisSagaMediator mediator) CreateMediator(string sagaName = "TestSaga")
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisLogger<SagaMediator>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaEngine>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaResumer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaDefinitionInitializer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaInstanceStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageLogStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageHandlerInvoker>>());

        services.AddAxisSagaPostgres(new AxisSagaSettings { ConnectionString = fixture.ConnectionString });

        services.AddSingleton(AxisSagaDefinitions.Define<TestPayload>(sagaName, saga =>
        {
            saga.AddStage("Step1").FinishOnSuccess();
        }));

        var sp = services.BuildServiceProvider();
        return (sp, sp.GetRequiredService<IAxisSagaMediator>());
    }

    [Fact]
    public async Task StartAsync_WithUnknownSagaName_ReturnsNotFound()
    {
        var (_, mediator) = CreateMediator();
        var result = await mediator.StartAsync("UnknownSaga", new TestPayload());

        result.ShouldFailWithCode(AxisSagaErrors.SagaDefinitionNotFound);
    }

    [Fact]
    public async Task StartAsync_PersistsInstance()
    {
        var (_, mediator) = CreateMediator();
        var payload = new TestPayload { Result = "initial" };

        var startResult = await mediator.StartAsync("TestSaga", payload);

        Assert.NotEmpty(startResult.ShouldSucceed());

        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            $"SELECT {SagaInstancesTable.SagaName} FROM {SagaInstancesTable.Table} " +
            $"WHERE {SagaInstancesTable.SagaId} = @id", conn);
        cmd.Parameters.AddWithValue("id", startResult.Value);
        var name = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal("TestSaga", name);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownSaga_ReturnsNotFound()
    {
        var (_, mediator) = CreateMediator();
        var unknown = Guid.CreateVersion7().ToString();

        var result = await mediator.GetByIdAsync(unknown);

        result.ShouldFailWithCode(AxisSagaErrors.SagaInstanceNotFound);
    }

    [Fact]
    public async Task GetByIdAsync_Typed_DeserializesPayload()
    {
        var (_, mediator) = CreateMediator();
        var payload = new TestPayload { Result = "marker-value" };

        var startResult = await mediator.StartAsync("TestSaga", payload);
        var result = await mediator.GetByIdAsync<TestPayload>(startResult.Value);

        Assert.Equal(startResult.Value, result.ShouldSucceed().SagaId);
    }

    // Regression: the fire-and-forget saga run must NOT inherit the caller's ExecutionContext. In the host
    // that context carries the request's ambient CancellationToken as an AsyncLocal, so inheriting it would
    // cancel the saga's first database call the instant the client disconnects after the 202. SuppressFlow
    // detaches the run, so an AsyncLocal set by the caller must not be observable inside the stage.
    private static readonly AsyncLocal<string?> CallerMarker = new();

    [Fact]
    public async Task FireAndForgetRun_DoesNotInheritTheCallersAmbientAsyncLocal()
    {
        var gate = new StageGate();
        await using var sp = BuildGatedProvider(gate);
        var mediator = sp.GetRequiredService<IAxisSagaMediator>();

        CallerMarker.Value = "caller";
        var start = await mediator.StartAsync("CtDetachSaga", new TestPayload { Result = "x" });
        start.ShouldSucceed();

        var observedInsideStage = await gate.MarkerInsideStage.Task
            .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        gate.Proceed.SetResult();
        Assert.Equal(AxisSagaStatus.Completed, await WaitForTerminalAsync(mediator, start.Value));

        Assert.Null(observedInsideStage);
    }

    // Regression: a fan-out enqueues more sagas than the cap allows; the excess is denied at the lease gate
    // and left Pending with no running task. When a running saga finishes and frees its slot, the mediator
    // must pump that deferred saga at once — not leave it for the periodic resumer (which is not running
    // here). With the cap pinned to 1 the second start is denied while the first holds the slot; releasing
    // the first must drive the second to completion on its own.
    [Fact]
    public async Task A_completing_saga_pumps_a_pending_saga_the_cap_had_deferred()
    {
        await ClearSagasAsync();
        await SetCapAsync(1);
        try
        {
            var gate = new StageGate();
            await using var sp = BuildGatedProvider(gate);
            var mediator = sp.GetRequiredService<IAxisSagaMediator>();

            var first = await mediator.StartAsync("CtDetachSaga", new TestPayload { Result = "first" });
            first.ShouldSucceed();
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

            var second = await mediator.StartAsync("CtDetachSaga", new TestPayload { Result = "second" });
            second.ShouldSucceed();
            var deferred = await mediator.GetByIdAsync(second.Value);
            Assert.Equal(AxisSagaStatus.Pending, deferred.ShouldSucceed().Status);

            gate.Proceed.SetResult();

            Assert.Equal(AxisSagaStatus.Completed, await WaitForTerminalAsync(mediator, first.Value));
            Assert.Equal(AxisSagaStatus.Completed, await WaitForTerminalAsync(mediator, second.Value));
        }
        finally
        {
            await SetCapAsync(null);
        }
    }

    private ServiceProvider BuildGatedProvider(StageGate gate)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisLogger<SagaMediator>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaEngine>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaResumer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaDefinitionInitializer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaInstanceStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageLogStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageHandlerInvoker>>());
        services.AddAxisSagaPostgres(new AxisSagaSettings { ConnectionString = fixture.ConnectionString });
        services.AddSingleton(AxisSagaDefinitions.Define<TestPayload>("CtDetachSaga", saga =>
        {
            saga.AddStage("Step1").FinishOnSuccess();
        }));
        services.AddSingleton(gate);
        services.AddScoped<IAxisSagaStageHandler<TestPayload>, GatedStageHandler>();
        return services.BuildServiceProvider();
    }

    // Sets the global cap (single SAGA_SETTINGS row); UPSERT so it works whether or not the seed ran.
    // null = unbounded — the convention for leaving the shared container's cap ungated between tests.
    private async Task SetCapAsync(int? cap)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            $"""
             INSERT INTO {SagaSettingsTable.Table} ({SagaSettingsTable.OnlyRow}, {SagaSettingsTable.MaxConcurrentSagas})
             VALUES (TRUE, @cap)
             ON CONFLICT ({SagaSettingsTable.OnlyRow}) DO UPDATE SET {SagaSettingsTable.MaxConcurrentSagas} = @cap
             """, conn);
        cmd.Parameters.AddWithValue("cap", (object?)cap ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // The global cap counts live leases across the whole shared table, so clear leftover sagas from other
    // tests before pinning the cap, exactly as SagaGlobalConcurrencyTests does.
    private async Task ClearSagasAsync()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand($"DELETE FROM {SagaInstancesTable.Table}", conn);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<AxisSagaStatus> WaitForTerminalAsync(IAxisSagaMediator mediator, string sagaId)
    {
        for (var i = 0; i < 200; i++)
        {
            var loaded = await mediator.GetByIdAsync(sagaId);
            if (loaded.IsSuccess && loaded.Value.Status is AxisSagaStatus.Completed or AxisSagaStatus.Failed)
                return loaded.Value.Status;
            await Task.Delay(50);
        }

        var final = await mediator.GetByIdAsync(sagaId);
        return final.IsSuccess ? final.Value.Status : AxisSagaStatus.Failed;
    }

    private sealed class StageGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Proceed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string?> MarkerInsideStage { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedStageHandler(StageGate gate) : IAxisSagaStageHandler<TestPayload>
    {
        public string SagaName => "CtDetachSaga";
        public string StageName => "Step1";

        public async Task<AxisResult<TestPayload>> ExecuteAsync(TestPayload payload)
        {
            gate.MarkerInsideStage.TrySetResult(CallerMarker.Value);
            gate.Entered.TrySetResult();
            await gate.Proceed.Task;
            return AxisResult.Ok(payload);
        }
    }
}
