using System.Data.Common;
using AxisBus.Postgres.Persistence;
using AxisCache.Postgres.Persistence;
using AxisSaga.Postgres.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using PostgresProvider = Scaffolds.ECommerce.Adapters.Driven.Postgres.DependencyInjection;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

public sealed class PostgresECommerceWebAppFactory : ECommerceWebAppFactory
{
    private readonly PostgreSqlContainer _container = NewContainer();

    protected override string ProviderName => "Postgres";

    protected override string ConnectionStringName => PostgresProvider.ConnectionStringName;

    protected override async Task<string> StartContainerAsync()
    {
        await _container.StartAsync();
        return ConnectionStringOf(_container);
    }

    protected override async Task MigrateFrameworkStoresAsync(string connectionString)
    {
        await AxisSagaMigrations.InitializePostgresAsync(connectionString);
        await AxisBusMigrations.InitializePostgresAsync(connectionString);
        await AxisCacheMigrations.InitializePostgresAsync(connectionString);
    }

    protected override Task StopContainerAsync() => _container.DisposeAsync().AsTask();

    protected override DbConnection NewConnection(string connectionString) => new NpgsqlConnection(connectionString);

    protected override DbConnectionStringBuilder NewConnectionStringBuilder(string connectionString)
        => new NpgsqlConnectionStringBuilder(connectionString);

    public override async Task<EmptyServer> StartEmptyServerAsync()
    {
        var container = NewContainer();
        await container.StartAsync();
        return new EmptyServer(ConnectionStringOf(container), container, NewConnection);
    }

    private static PostgreSqlContainer NewContainer() => new PostgreSqlBuilder("postgres:18.6")
        .WithDatabase("ecommerce_e2e").WithUsername("e2e_test").WithPassword("password_test").WithCleanUp(true).Build();

    private static string ConnectionStringOf(PostgreSqlContainer container)
        => new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            MaxPoolSize = MaxPoolSizeWithinContainerConnectionLimit,
        }.ConnectionString;
}
