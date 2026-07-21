using Testcontainers.PostgreSql;

namespace AxisRepository.Postgres.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:latest")
        .WithDatabase("axis_repo_pg_test")
        .WithUsername("admin_test")
        .WithPassword("password_test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("AxisRepositoryPostgresCollection")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
