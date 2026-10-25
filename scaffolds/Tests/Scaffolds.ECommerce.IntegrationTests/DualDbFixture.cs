using AxisSaga.MySql.Persistence;
using AxisSaga.Postgres.Persistence;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Npgsql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;
using MySqlBusMigrations = AxisBus.MySql.Persistence.AxisBusMigrations;
using MySqlCacheMigrations = AxisCache.MySql.Persistence.AxisCacheMigrations;
using MySqlProvider = Scaffolds.ECommerce.Adapters.Driven.MySql.DependencyInjection;
using PostgresBusMigrations = AxisBus.Postgres.Persistence.AxisBusMigrations;
using PostgresCacheMigrations = AxisCache.Postgres.Persistence.AxisCacheMigrations;
using PostgresProvider = Scaffolds.ECommerce.Adapters.Driven.Postgres.DependencyInjection;

namespace Scaffolds.ECommerce.IntegrationTests;

public sealed class DualDbFixture : IAsyncLifetime
{
    private const int MaxPoolSizeWithinContainerConnectionLimit = 20;
    private const string WrongPassword = "not-the-password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6")
        .WithDatabase("ecommerce_pg").WithUsername("admin_test").WithPassword("password_test").WithCleanUp(true).Build();

    private readonly MySqlContainer _mysql = new MySqlBuilder("mysql:8.4.10")
        .WithDatabase("ecommerce_mysql").WithUsername("root").WithPassword("password_test").WithCleanUp(true).Build();

    public string PostgresConnectionString => new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
    {
        MaxPoolSize = MaxPoolSizeWithinContainerConnectionLimit,
    }.ConnectionString;

    public string MySqlConnectionString => new MySqlConnectionStringBuilder(_mysql.GetConnectionString())
    {
        MaximumPoolSize = MaxPoolSizeWithinContainerConnectionLimit,
    }.ConnectionString;

    public string ConnectionString(DbProvider provider)
        => provider == DbProvider.MySql ? MySqlConnectionString : PostgresConnectionString;

    public string UnreachableConnectionString(DbProvider provider)
        => provider == DbProvider.MySql
            ? new MySqlConnectionStringBuilder(MySqlConnectionString) { Password = WrongPassword }.ConnectionString
            : new NpgsqlConnectionStringBuilder(PostgresConnectionString) { Password = WrongPassword }.ConnectionString;

    public IConfiguration Configuration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{PostgresProvider.ConnectionStringName}"] = PostgresConnectionString,
                [$"ConnectionStrings:{MySqlProvider.ConnectionStringName}"] = MySqlConnectionString,
            })
            .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mysql.StartAsync());
        await Task.WhenAll(MigrateAsync(DbProvider.Postgres), MigrateAsync(DbProvider.MySql));
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _mysql.DisposeAsync();
    }

    private async Task MigrateAsync(DbProvider provider)
    {
        await MigrateThroughThePortAsync(provider);
        await MigrateFrameworkStoresAsync(provider);
    }

    private async Task MigrateThroughThePortAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(this, provider);
        using var scope = services.CreateScope();
        var databaseMigrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigratorPort>();

        (await databaseMigrator.MigrateAsync()).ShouldSucceed();
    }

    private async Task MigrateFrameworkStoresAsync(DbProvider provider)
    {
        if (provider == DbProvider.MySql)
        {
            await AxisSagaMySqlMigrations.InitializeMySqlAsync(MySqlConnectionString);
            await MySqlBusMigrations.InitializeMySqlAsync(MySqlConnectionString);
            await MySqlCacheMigrations.InitializeMySqlAsync(MySqlConnectionString);
            return;
        }

        await AxisSagaMigrations.InitializePostgresAsync(PostgresConnectionString);
        await PostgresBusMigrations.InitializePostgresAsync(PostgresConnectionString);
        await PostgresCacheMigrations.InitializePostgresAsync(PostgresConnectionString);
    }
}
