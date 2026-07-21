using Axis.Contracts;
using Axis.Contracts.Configuration;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.Postgres.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace AxisSaga.Postgres.IntegrationTests;

/// <summary>
/// Regression guard for the per-stage DI scope. A forward stage that faults its scoped unit of work
/// (a duplicate key aborts the Postgres transaction in production) must NOT poison the compensation
/// chain: each stage runs in its own scope, so the compensation stage resolves a fresh unit of work
/// and can actually run. Before the fix the whole saga shared one scope, the faulted unit of work was
/// reused, and compensation failed with POSTGRES_TRANSACTION_FAULTED — leaving the run stuck.
/// </summary>
[Collection("AxisSagaPostgresCollection")]
public class StageScopeIsolationTests(AxisSagaPostgresFixture fixture)
{
    private const string SagaName = "ScopeIsolationSaga";
    private const string ForwardStage = "Forward";
    private const string CompensateStage = "Compensate";

    public record ScopeTestPayload
    {
        public string? Marker { get; init; }
    }

    // Scoped: one instance per DI scope. The mutable flag mimics PostgresUnitOfWork.IsFaulted, which
    // latches after a failed statement and makes every later call on the same instance short-circuit.
    public sealed class ScopeProbe
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
        public bool Faulted { get; set; }
    }

    // Singleton: shared across every stage scope so the test can observe what each stage's scope saw.
    public sealed class ProbeSink
    {
        public Guid ForwardProbeInstanceId { get; set; }
        public Guid CompensationProbeInstanceId { get; set; }
        public bool CompensationObservedFaulted { get; set; }
    }

    public sealed class ForwardHandler(ScopeProbe probe, ProbeSink sink)
        : IAxisSagaStageHandler<ScopeTestPayload>
    {
        string IAxisSagaStageHandler<ScopeTestPayload>.SagaName => SagaName;
        public string StageName => ForwardStage;

        public Task<AxisResult<ScopeTestPayload>> ExecuteAsync(ScopeTestPayload payload)
        {
            // Fault this scope's unit of work, then fail so the engine kicks off compensation.
            probe.Faulted = true;
            sink.ForwardProbeInstanceId = probe.InstanceId;
            AxisResult<ScopeTestPayload> failure = AxisError.InternalServerError("FORWARD_FAILED");
            return Task.FromResult(failure);
        }
    }

    public sealed class CompensationHandler(ScopeProbe probe, ProbeSink sink)
        : IAxisSagaStageHandler<ScopeTestPayload>
    {
        string IAxisSagaStageHandler<ScopeTestPayload>.SagaName => SagaName;
        public string StageName => CompensateStage;

        public Task<AxisResult<ScopeTestPayload>> ExecuteAsync(ScopeTestPayload payload)
        {
            // With a shared saga scope this probe is the SAME faulted instance the forward stage
            // poisoned; with a per-stage scope it is a fresh, clean probe.
            sink.CompensationProbeInstanceId = probe.InstanceId;
            sink.CompensationObservedFaulted = probe.Faulted;
            return Task.FromResult(AxisResult.Ok(payload));
        }
    }

    [Fact]
    public async Task CompensationStageRunsInFreshScopeNotPoisonedByFailedForwardStageAsync()
    {
        var sink = new ProbeSink();
        var sp = BuildServiceProvider(sink);
        var mediator = sp.GetRequiredService<IAxisSagaMediator>();

        var start = await mediator.StartAsync(SagaName, new ScopeTestPayload { Marker = "x" });
        start.ShouldSucceed();

        var status = await WaitForTerminalAsync(mediator, start.Value);

        // The compensation chain completed end to end → the saga reached Compensated.
        Assert.Equal(AxisSagaStatus.Compensated, status);
        // The compensation stage saw a clean unit of work, not the forward stage's faulted one.
        Assert.False(sink.CompensationObservedFaulted);
        // ...because it was resolved from a different DI scope than the forward stage.
        Assert.NotEqual(sink.ForwardProbeInstanceId, sink.CompensationProbeInstanceId);
    }

    private IServiceProvider BuildServiceProvider(ProbeSink sink)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisLogger<SagaMediator>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaEngine>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaInstanceStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageLogStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageHandlerInvoker>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaResumer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaDefinitionInitializer>>());

        services.AddAxisSagaPostgres(new AxisSagaSettings { ConnectionString = fixture.ConnectionString });

        services.AddSingleton(sink);
        services.AddScoped<ScopeProbe>();
        services.AddScoped<IAxisSagaStageHandler<ScopeTestPayload>, ForwardHandler>();
        services.AddScoped<IAxisSagaStageHandler<ScopeTestPayload>, CompensationHandler>();

        services.AddSingleton(AxisSagaDefinitions.Define<ScopeTestPayload>(SagaName, saga =>
        {
            saga.AddStage(ForwardStage).RouteToOnError(CompensateStage);
            saga.AddErrorStage(CompensateStage).FinishOnSuccess();
        }));

        return services.BuildServiceProvider();
    }

    private static async Task<AxisSagaStatus> WaitForTerminalAsync(IAxisSagaMediator mediator, string sagaId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var instance = await mediator.GetByIdAsync(sagaId);
            if (instance.IsSuccess && IsTerminal(instance.Value.Status))
                return instance.Value.Status;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Saga {sagaId} did not reach a terminal status in time.");
    }

    private static bool IsTerminal(AxisSagaStatus status)
        => status is AxisSagaStatus.Completed or AxisSagaStatus.Failed or AxisSagaStatus.Compensated;
}
