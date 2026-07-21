using Axis.Contracts;
using Axis.Contracts.Configuration;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisRepository.Postgres;
using AxisSaga.Postgres.Adapters;
using AxisSaga.Postgres.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisSaga.Postgres.IntegrationTests;

/// <summary>
/// The keyed (per-subdomain) Postgres saga adapter: several stores keyed by serviceKey coexist in one
/// process, each guarded once per key, reusing AxisRepository's keyed datasource when present, with
/// definitions isolated per key. Mirrors the keyed AddPostgresUnitOfWork convention of AxisRepository.
/// </summary>
[Collection("AxisSagaPostgresCollection")]
public class AxisSagaPostgresKeyedTests(AxisSagaPostgresFixture fixture)
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
        services.AddSingleton(Mock.Of<IAxisLogger<SagaInstanceStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageLogStore>>());
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
        services.AddAxisSagaPostgres(KeyA, Settings());
        services.AddAxisSagaPostgres(KeyB, Settings());   // second key must NOT trip the per-process guard

        await using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredKeyedService<IAxisSagaMediator>(KeyA));
        Assert.NotNull(sp.GetRequiredKeyedService<IAxisSagaMediator>(KeyB));
    }

    [Fact]
    public void SameKeyRegisteredTwiceThrows()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddAxisSagaPostgres(KeyA, Settings());

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddAxisSagaPostgres(KeyA, Settings()));
        Assert.Contains("already been registered", ex.Message);
        Assert.Contains(KeyA, ex.Message);
    }

    [Fact]
    public async Task KeyedSagaReusesRepositoryDataSourceWhenRegisteredAsync()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddPostgresUnitOfWork(KeyA, fixture.ConnectionString);   // BC's keyed NpgsqlDataSource
        services.AddAxisSagaPostgres(KeyA, Settings());

        await using var sp = services.BuildServiceProvider();
        var repositoryDataSource = sp.GetRequiredKeyedService<NpgsqlDataSource>(KeyA);
        var sagaDataSource = sp.GetRequiredKeyedService<AxisSagaPostgresDataSource>(KeyA);

        Assert.Same(repositoryDataSource, sagaDataSource.Inner);   // one pool shared by repository + saga
    }

    [Fact]
    public async Task KeyedSagaOwnsItsDataSourceWhenNoRepositoryAsync()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddAxisSagaPostgres(KeyA, Settings());

        await using var sp = services.BuildServiceProvider();
        Assert.Null(sp.GetKeyedService<NpgsqlDataSource>(KeyA));   // repository registered none
        Assert.NotNull(sp.GetRequiredKeyedService<AxisSagaPostgresDataSource>(KeyA).Inner);
    }

    [Fact]
    public async Task KeyedMediatorRunsSagaToCompletionAsync()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddAxisSagaPostgres(KeyA, Settings());
        services.AddKeyedSingleton(KeyA, AxisSagaDefinitions.Define<KeyedPayload>("KeyedRoundTrip",
            saga => saga.AddStage("Step1").FinishOnSuccess()));
        services.AddScoped<IAxisSagaStageHandler<KeyedPayload>>(_ => new NamedHandler("KeyedRoundTrip", "Step1"));

        await using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredKeyedService<IAxisSagaMediator>(KeyA);

        var start = await mediator.StartAsync("KeyedRoundTrip", new KeyedPayload { Result = "x" });
        start.ShouldSucceed();
        Assert.Equal(AxisSagaStatus.Completed, await WaitForTerminalAsync(mediator, start.Value));
    }

    [Fact]
    public async Task DefinitionsAreIsolatedPerKeyAsync()
    {
        var services = new ServiceCollection();
        AddLoggers(services);
        services.AddAxisSagaPostgres(KeyA, Settings());
        services.AddAxisSagaPostgres(KeyB, Settings());
        // The definition is registered ONLY under KeyA's registry.
        services.AddKeyedSingleton(KeyA, AxisSagaDefinitions.Define<KeyedPayload>("OnlyInA",
            saga => saga.AddStage("Step1").FinishOnSuccess()));

        await using var sp = services.BuildServiceProvider();

        // Per-key registry isolation: KeyA resolves the definition, KeyB does not.
        Assert.NotNull(sp.GetRequiredKeyedService<IAxisSagaDefinitionRegistry>(KeyA).Get("OnlyInA"));
        Assert.Null(sp.GetRequiredKeyedService<IAxisSagaDefinitionRegistry>(KeyB).Get("OnlyInA"));

        // End-to-end: starting the KeyA-only saga on KeyB's mediator is rejected before any write.
        var onB = await sp.GetRequiredKeyedService<IAxisSagaMediator>(KeyB).StartAsync("OnlyInA", new KeyedPayload());
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
