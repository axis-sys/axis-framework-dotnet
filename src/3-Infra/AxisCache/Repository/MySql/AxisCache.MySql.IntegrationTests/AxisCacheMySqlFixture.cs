using AxisCache.MySql.Persistence;
using Testcontainers.MySql;

namespace AxisCache.MySql.IntegrationTests;

public sealed class AxisCacheMySqlFixture : IAsyncLifetime
{
    // Root so the migration runner can CREATE SCHEMA (a database) for AXIS_CACHE. 8.4 is the LTS line and
    // supports the INSERT ... AS new row alias the upsert relies on.
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
        .WithDatabase("axis_cache_test")
        .WithUsername("root")
        .WithPassword("password_test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await AxisCacheMigrations.InitializeMySqlAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("AxisCacheMySqlCollection")]
public class AxisCacheMySqlCollection : ICollectionFixture<AxisCacheMySqlFixture>;
