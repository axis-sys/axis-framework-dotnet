using Axis.Contracts;
using Axis.Contracts.Configuration;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.MySql.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace AxisSaga.MySql.IntegrationTests;

/// <summary>
/// End-to-end lifecycle of a saga driven entirely through the MySQL adapter: a happy path that reaches
/// Completed, and a forward failure that routes to the compensation chain and reaches Compensated. Each
/// exercises the full SQL surface — Insert, AcquireLease (UPDATE + SELECT), PersistStageSuccess,
/// MoveToStatus, Complete/Compensate, the stage logs, and payload reload — against a real MySQL.
/// </summary>
[Collection("AxisSagaMySqlCollection")]
public class MySqlSagaLifecycleTests(AxisSagaMySqlFixture fixture)
{
    public record Payload
    {
        public string? Marker { get; init; }
    }

    public sealed class HappyHandler : IAxisSagaStageHandler<Payload>
    {
        public string SagaName => "MySqlHappySaga";
        public string StageName => "OnlyStage";
        public Task<AxisResult<Payload>> ExecuteAsync(Payload payload)
            => Task.FromResult(AxisResult.Ok(new Payload { Marker = "done" }));
    }

    public sealed class FailingForwardHandler : IAxisSagaStageHandler<Payload>
    {
        public string SagaName => "MySqlCompSaga";
        public string StageName => "Forward";
        public Task<AxisResult<Payload>> ExecuteAsync(Payload payload)
        {
            AxisResult<Payload> failure = AxisError.InternalServerError("FORWARD_FAILED");
            return Task.FromResult(failure);
        }
    }

    public sealed class CompensateHandler : IAxisSagaStageHandler<Payload>
    {
        public string SagaName => "MySqlCompSaga";
        public string StageName => "Compensate";
        public Task<AxisResult<Payload>> ExecuteAsync(Payload payload)
            => Task.FromResult(AxisResult.Ok(payload));
    }

    [Fact]
    public async Task HappyPathSagaReachesCompletedAsync()
    {
        var sp = BuildSp(services =>
        {
            services.AddScoped<IAxisSagaStageHandler<Payload>, HappyHandler>();
            services.AddSingleton(AxisSagaDefinitions.Define<Payload>("MySqlHappySaga", saga =>
                saga.AddStage("OnlyStage").FinishOnSuccess()));
        });
        var mediator = sp.GetRequiredService<IAxisSagaMediator>();

        var start = await mediator.StartAsync("MySqlHappySaga", new Payload { Marker = "x" });
        start.ShouldSucceed();

        var status = await WaitForTerminalAsync(mediator, start.Value);
        Assert.Equal(AxisSagaStatus.Completed, status);
    }

    [Fact]
    public async Task ForwardFailureRoutesToCompensationAndReachesCompensatedAsync()
    {
        var sp = BuildSp(services =>
        {
            services.AddScoped<IAxisSagaStageHandler<Payload>, FailingForwardHandler>();
            services.AddScoped<IAxisSagaStageHandler<Payload>, CompensateHandler>();
            services.AddSingleton(AxisSagaDefinitions.Define<Payload>("MySqlCompSaga", saga =>
            {
                saga.AddStage("Forward").RouteToOnError("Compensate");
                saga.AddErrorStage("Compensate").FinishOnSuccess();
            }));
        });
        var mediator = sp.GetRequiredService<IAxisSagaMediator>();

        var start = await mediator.StartAsync("MySqlCompSaga", new Payload());
        start.ShouldSucceed();

        var status = await WaitForTerminalAsync(mediator, start.Value);
        Assert.Equal(AxisSagaStatus.Compensated, status);
    }

    private IServiceProvider BuildSp(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisLogger<SagaMediator>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaEngine>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaResumer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaDefinitionInitializer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<MySqlSagaInstanceStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<MySqlSagaStageLogStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageHandlerInvoker>>());
        services.AddAxisSagaMySql(new AxisSagaSettings
        {
            ConnectionString = fixture.ConnectionString,
            ResumerEnabled = false,
            ResumeAfter = TimeSpan.FromMinutes(5),
        });
        configure(services);
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
