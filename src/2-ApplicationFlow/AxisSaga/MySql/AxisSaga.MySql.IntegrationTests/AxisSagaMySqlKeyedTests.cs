using Axis.Contracts;
using Axis.Contracts.Configuration;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.MySql.Adapters;
using AxisSaga.MySql.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AxisSaga.MySql.IntegrationTests;

/// <summary>
/// The keyed (per-subdomain) MySQL saga adapter: several stores keyed by serviceKey coexist in one
/// process, guarded once per key, each owning its own READ COMMITTED datasource, with definitions
/// isolated per key. Mirrors the keyed AddMySqlUnitOfWork convention of AxisRepository.
/// </summary>
[Collection("AxisSagaMySqlCollection")]
public class AxisSagaMySqlKeyedTests(AxisSagaMySqlFixture fixture)
{
    private const string KeyA = "ecommerce";
    private const string KeyB = "financas";

    public record KeyedPayload
    {
        public string? Result { get; init; }
    }

    private static void AddLoggers(IServiceCollection services)
    {
        services.AddSingleton(Mock.Of<IAxisLogger<SagaMediator>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaEngine>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaResumer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaDefinitionInitializer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<MySqlSagaInstanceStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<MySqlSagaStageLogStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageHandlerInvoker>>());
    }

    private AxisSagaSettings Settings() => new()
    {
        ConnectionString = fixture.ConnectionString,
        ResumerEnabled = false,
        ResumeAfter = TimeSpan.FromMinutes(5),
    };

    [Fact]
    public async Task TwoKeysCoexistWithoutThrowingAsync()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddAxisSagaMySql(KeyA, Settings());
        services.AddAxisSagaMySql(KeyB, Settings());   // second key must NOT trip the per-process guard

        await using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredKeyedService<IAxisSagaMediator>(KeyA));
        Assert.NotNull(sp.GetRequiredKeyedService<IAxisSagaMediator>(KeyB));
    }

    [Fact]
    public void SameKeyRegisteredTwiceThrows()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddAxisSagaMySql(KeyA, Settings());

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddAxisSagaMySql(KeyA, Settings()));
        Assert.Contains("already been registered", ex.Message);
        Assert.Contains(KeyA, ex.Message);
    }

    [Fact]
    public async Task EachKeyOwnsADistinctDataSourceAsync()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddAxisSagaMySql(KeyA, Settings());
        services.AddAxisSagaMySql(KeyB, Settings());

        await using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredKeyedService<AxisSagaMySqlDataSource>(KeyA);
        var b = sp.GetRequiredKeyedService<AxisSagaMySqlDataSource>(KeyB);

        Assert.NotSame(a, b);
        Assert.NotSame(a.Inner, b.Inner);
    }

    [Fact]
    public async Task KeyedMediatorRunsSagaToCompletionAsync()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddAxisSagaMySql(KeyA, Settings());
        services.AddKeyedSingleton(KeyA, AxisSagaDefinitions.Define<KeyedPayload>("MySqlKeyedRoundTrip",
            saga => saga.AddStage("Step1").FinishOnSuccess()));
        services.AddScoped<IAxisSagaStageHandler<KeyedPayload>>(_ => new NamedHandler("MySqlKeyedRoundTrip", "Step1"));

        await using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredKeyedService<IAxisSagaMediator>(KeyA);

        var start = await mediator.StartAsync("MySqlKeyedRoundTrip", new KeyedPayload { Result = "x" });
        start.ShouldSucceed();
        Assert.Equal(AxisSagaStatus.Completed, await WaitForTerminalAsync(mediator, start.Value));
    }

    [Fact]
    public async Task DefinitionsAreIsolatedPerKeyAsync()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddAxisSagaMySql(KeyA, Settings());
        services.AddAxisSagaMySql(KeyB, Settings());
        services.AddKeyedSingleton(KeyA, AxisSagaDefinitions.Define<KeyedPayload>("MySqlOnlyInA",
            saga => saga.AddStage("Step1").FinishOnSuccess()));

        await using var sp = services.BuildServiceProvider();

        // Per-key registry isolation: KeyA resolves the definition, KeyB does not.
        Assert.NotNull(sp.GetRequiredKeyedService<IAxisSagaDefinitionRegistry>(KeyA).Get("MySqlOnlyInA"));
        Assert.Null(sp.GetRequiredKeyedService<IAxisSagaDefinitionRegistry>(KeyB).Get("MySqlOnlyInA"));

        // End-to-end: starting the KeyA-only saga on KeyB's mediator is rejected before any write.
        var onB = await sp.GetRequiredKeyedService<IAxisSagaMediator>(KeyB).StartAsync("MySqlOnlyInA", new KeyedPayload());
        onB.ShouldFailWithCode(AxisSagaErrors.SagaDefinitionNotFound);
    }

    private sealed class NamedHandler(string sagaName, string stageName) : IAxisSagaStageHandler<KeyedPayload>
    {
        public string SagaName => sagaName;
        public string StageName => stageName;
        public Task<AxisResult<KeyedPayload>> ExecuteAsync(KeyedPayload payload) => Task.FromResult(AxisResult.Ok(payload));
    }

    private static async Task<AxisSagaStatus> WaitForTerminalAsync(IAxisSagaMediator mediator, string sagaId)
    {
        for (var i = 0; i < 200; i++)
        {
            var loaded = await mediator.GetByIdAsync(sagaId);
            if (loaded.IsSuccess && loaded.Value.Status is AxisSagaStatus.Completed or AxisSagaStatus.Failed or AxisSagaStatus.Compensated)
                return loaded.Value.Status;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        var final = await mediator.GetByIdAsync(sagaId);
        return final.IsSuccess ? final.Value.Status : AxisSagaStatus.Failed;
    }
}
