using AxisBus.Postgres.Persistence;
using Testcontainers.PostgreSql;

namespace AxisBus.Postgres.IntegrationTests;

public sealed class AxisBusPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:latest")
        .WithDatabase("axis_bus_test")
        .WithUsername("admin_test")
        .WithPassword("password_test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await AxisBusMigrations.InitializePostgresAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("AxisBusPostgresCollection")]
public class AxisBusPostgresCollection : ICollectionFixture<AxisBusPostgresFixture>;
