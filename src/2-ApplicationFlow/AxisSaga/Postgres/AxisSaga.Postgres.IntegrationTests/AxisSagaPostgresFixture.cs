using AxisSaga.Postgres.Persistence;
using Testcontainers.PostgreSql;

namespace AxisSaga.Postgres.IntegrationTests;

public sealed class AxisSagaPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:latest")
        .WithDatabase("axis_saga_test")
        .WithUsername("admin_test")
        .WithPassword("password_test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await AxisSagaMigrations.InitializePostgresAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("AxisSagaPostgresCollection")]
public class AxisSagaPostgresCollection : ICollectionFixture<AxisSagaPostgresFixture>;
