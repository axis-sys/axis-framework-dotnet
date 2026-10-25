using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Npgsql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;
using MySqlProvider = Scaffolds.ECommerce.Adapters.Driven.MySql.DependencyInjection;
using PostgresProvider = Scaffolds.ECommerce.Adapters.Driven.Postgres.DependencyInjection;

namespace Scaffolds.ECommerce.IntegrationTests;

internal sealed class EmptyServer : IAsyncDisposable
{
    private readonly DbProvider _provider;
    private readonly IDatabaseContainer _container;

    private EmptyServer(DbProvider provider, IDatabaseContainer container)
    {
        _provider = provider;
        _container = container;
    }

    public string ConnectionString => _container.GetConnectionString();

    public IConfiguration Configuration => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{(_provider == DbProvider.MySql ? MySqlProvider.ConnectionStringName : PostgresProvider.ConnectionStringName)}"] = ConnectionString,
        })
        .Build();

    public static async Task<EmptyServer> StartAsync(DbProvider provider)
    {
        IDatabaseContainer container = provider == DbProvider.MySql
            ? new MySqlBuilder("mysql:8.4.10").WithDatabase("ecommerce_empty").WithUsername("root").WithPassword("password_test").WithCleanUp(true).Build()
            : new PostgreSqlBuilder("postgres:18.6").WithDatabase("ecommerce_empty").WithUsername("admin_test").WithPassword("password_test").WithCleanUp(true).Build();
        await container.StartAsync(TestContext.Current.CancellationToken);
        return new EmptyServer(provider, container);
    }

    public async Task<long> CountTablesAsync(string schema)
    {
        await using DbConnection connection = _provider == DbProvider.MySql ? new MySqlConnection(ConnectionString) : new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE UPPER(table_schema) = @schema";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "schema";
        parameter.Value = schema;
        command.Parameters.Add(parameter);

        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
