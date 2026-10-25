using System.Data.Common;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

public sealed class EmptyServer(string connectionString, IAsyncDisposable container, Func<string, DbConnection> newConnection) : IAsyncDisposable
{
    public string ConnectionString { get; } = connectionString;

    public async Task<long> CountTablesAsync(string schema)
    {
        await using var connection = newConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE UPPER(table_schema) = @schema";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "schema";
        parameter.Value = schema;
        command.Parameters.Add(parameter);

        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
