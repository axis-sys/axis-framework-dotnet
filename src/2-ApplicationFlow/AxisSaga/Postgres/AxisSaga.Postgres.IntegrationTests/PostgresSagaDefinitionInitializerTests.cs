using Axis.Contracts.Configuration;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.Saga;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisSaga.Postgres.IntegrationTests;

[Collection("AxisSagaPostgresCollection")]
public class PostgresSagaDefinitionInitializerTests(AxisSagaPostgresFixture fixture)
{
    public record TestPayload
    {
        public string Marker { get; init; } = "";
    }

    private ServiceProvider BuildProvider(string sagaName, Action<IAxisSagaConfigurator<TestPayload>>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisLogger<SagaMediator>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaEngine>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaResumer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaDefinitionInitializer>>());

        services.AddAxisSagaPostgres(new AxisSagaSettings { ConnectionString = fixture.ConnectionString });

        services.AddSingleton(AxisSagaDefinitions.Define(sagaName, configure ?? (s => s.AddStage("S1").FinishOnSuccess())));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InitializeAsync_FirstRun_InsertsDefinition()
    {
        var sagaName = $"InitFirst_{Guid.NewGuid():N}";
        await using var sp = BuildProvider(sagaName);

        using var scope = sp.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IAxisSagaDefinitionInitializer>();

        var written = await initializer.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, written);
        await AssertDefinitionExistsAsync(sagaName);
    }

    [Fact]
    public async Task InitializeAsync_SameDefinition_IsIdempotent()
    {
        var sagaName = $"InitIdem_{Guid.NewGuid():N}";
        await using var sp = BuildProvider(sagaName);
        using var scope = sp.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IAxisSagaDefinitionInitializer>();

        var first = await initializer.InitializeAsync(CancellationToken.None);
        var second = await initializer.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task InitializeAsync_ChangedDefinition_UpdatesHash()
    {
        var sagaName = $"InitChanged_{Guid.NewGuid():N}";

        await using (var sp1 = BuildProvider(sagaName, s => s.AddStage("OldStage").FinishOnSuccess()))
        {
            using var scope = sp1.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<IAxisSagaDefinitionInitializer>();
            var first = await initializer.InitializeAsync(CancellationToken.None);
            Assert.Equal(1, first);
        }

        await using var sp2 = BuildProvider(sagaName, s => s.AddStage("NewStage").FinishOnSuccess());
        using var scope2 = sp2.CreateScope();
        var initializer2 = scope2.ServiceProvider.GetRequiredService<IAxisSagaDefinitionInitializer>();

        var second = await initializer2.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, second);   // 1 update because hash changed
    }

    private async Task AssertDefinitionExistsAsync(string sagaName)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"SELECT 1 FROM {SagaDefinitionsTable.Table} WHERE {SagaDefinitionsTable.SagaName} = @name",
            conn);
        cmd.Parameters.AddWithValue("name", sagaName);
        Assert.NotNull(await cmd.ExecuteScalarAsync());
    }
}
