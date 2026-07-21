using Testcontainers.MySql;

namespace AxisRepository.MySql.IntegrationTests;

public sealed class MySqlFixture : IAsyncLifetime
{
    // Root so the tests can CREATE SCHEMA (a database) for the sandbox tables; 8.4 is the LTS line.
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
        .WithDatabase("axis_repo_mysql_test")
        .WithUsername("root")
        .WithPassword("password_test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("AxisRepositoryMySqlCollection")]
public class MySqlCollection : ICollectionFixture<MySqlFixture>;
