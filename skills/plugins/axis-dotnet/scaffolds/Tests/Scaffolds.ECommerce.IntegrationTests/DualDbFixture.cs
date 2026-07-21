using Testcontainers.MySql;
using Testcontainers.PostgreSql;

namespace Scaffolds.ECommerce.IntegrationTests;

public sealed class DualDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:latest")
        .WithDatabase("ecommerce_pg").WithUsername("admin_test").WithPassword("password_test").WithCleanUp(true).Build();

    private readonly MySqlContainer _mysql = new MySqlBuilder("mysql:8.4")
        .WithDatabase("ecommerce_mysql").WithUsername("root").WithPassword("password_test").WithCleanUp(true).Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();
    public string MySqlConnectionString => _mysql.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mysql.StartAsync());
        await EComMigrations.InitializeAsync(PostgresConnectionString, new PostgresSqlDialect(), new PostgresMigrationRunner());
        await EComMigrations.InitializeAsync(MySqlConnectionString, new MySqlSqlDialect(), new MySqlMigrationRunner());
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _mysql.DisposeAsync();
    }
}

[CollectionDefinition("DualDbECommerceCollection")]
public class DualDbCollection : ICollectionFixture<DualDbFixture>;
