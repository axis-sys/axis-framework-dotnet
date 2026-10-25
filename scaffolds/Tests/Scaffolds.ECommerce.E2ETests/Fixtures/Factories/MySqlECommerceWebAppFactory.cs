using System.Data.Common;
using AxisBus.MySql.Persistence;
using AxisCache.MySql.Persistence;
using AxisSaga.MySql.Persistence;
using MySqlConnector;
using Testcontainers.MySql;
using MySqlProvider = Scaffolds.ECommerce.Adapters.Driven.MySql.DependencyInjection;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

public sealed class MySqlECommerceWebAppFactory : ECommerceWebAppFactory
{
    private readonly MySqlContainer _container = NewContainer();

    protected override string ProviderName => "MySql";

    protected override string ConnectionStringName => MySqlProvider.ConnectionStringName;

    protected override async Task<string> StartContainerAsync()
    {
        await _container.StartAsync();
        return ConnectionStringOf(_container);
    }

    protected override async Task MigrateFrameworkStoresAsync(string connectionString)
    {
        await AxisSagaMySqlMigrations.InitializeMySqlAsync(connectionString);
        await AxisBusMigrations.InitializeMySqlAsync(connectionString);
        await AxisCacheMigrations.InitializeMySqlAsync(connectionString);
    }

    protected override Task StopContainerAsync() => _container.DisposeAsync().AsTask();

    protected override DbConnection NewConnection(string connectionString) => new MySqlConnection(connectionString);

    protected override DbConnectionStringBuilder NewConnectionStringBuilder(string connectionString)
        => new MySqlConnectionStringBuilder(connectionString);

    public override async Task<EmptyServer> StartEmptyServerAsync()
    {
        var container = NewContainer();
        await container.StartAsync();
        return new EmptyServer(ConnectionStringOf(container), container, NewConnection);
    }

    private static MySqlContainer NewContainer() => new MySqlBuilder("mysql:8.4.10")
        .WithDatabase("ecommerce_e2e").WithUsername("root").WithPassword("password_test").WithCleanUp(true).Build();

    private static string ConnectionStringOf(MySqlContainer container)
        => new MySqlConnectionStringBuilder(container.GetConnectionString())
        {
            MaximumPoolSize = MaxPoolSizeWithinContainerConnectionLimit,
        }.ConnectionString;
}
