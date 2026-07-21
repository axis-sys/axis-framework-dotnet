using AxisSaga.MySql.Persistence;
using Testcontainers.MySql;

namespace AxisSaga.MySql.IntegrationTests;

public sealed class AxisSagaMySqlFixture : IAsyncLifetime
{
    // Root so the migration runner can CREATE SCHEMA (a database) for AXIS_SAGA. 8.4 is the LTS line and
    // supports everything the adapter relies on (expression column defaults, CHECK, derived tables, the
    // INSERT ... AS new row alias).
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
        .WithDatabase("axis_saga_test")
        .WithUsername("root")
        .WithPassword("password_test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await AxisSagaMySqlMigrations.InitializeMySqlAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("AxisSagaMySqlCollection")]
public class AxisSagaMySqlCollection : ICollectionFixture<AxisSagaMySqlFixture>;
