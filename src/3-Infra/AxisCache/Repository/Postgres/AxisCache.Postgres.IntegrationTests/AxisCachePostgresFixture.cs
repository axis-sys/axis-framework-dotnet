using AxisCache.Postgres.Persistence;
using Testcontainers.PostgreSql;

namespace AxisCache.Postgres.IntegrationTests;

public sealed class AxisCachePostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:latest")
        .WithDatabase("axis_cache_test")
        .WithUsername("admin_test")
        .WithPassword("password_test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await AxisCacheMigrations.InitializePostgresAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("AxisCachePostgresCollection")]
public class AxisCachePostgresCollection : ICollectionFixture<AxisCachePostgresFixture>;
