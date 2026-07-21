using AxisBus.MySql.Persistence;
using Testcontainers.MySql;

namespace AxisBus.MySql.IntegrationTests;

public sealed class AxisBusMySqlFixture : IAsyncLifetime
{
    // Root so the migration runner can CREATE SCHEMA (a database) for AXIS_BUS. 8.4 is the LTS line, matching
    // the other MySQL-backed framework fixtures (AxisCache, AxisSaga).
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
        .WithDatabase("axis_bus_test")
        .WithUsername("root")
        .WithPassword("password_test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await AxisBusMigrations.InitializeMySqlAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("AxisBusMySqlCollection")]
public class AxisBusMySqlCollection : ICollectionFixture<AxisBusMySqlFixture>;
