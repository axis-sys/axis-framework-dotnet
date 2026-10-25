using System.Data.Common;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

public sealed class RowLock : IAsyncDisposable
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;

    private RowLock(DbConnection connection, DbTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public static async Task<RowLock> AcquireAsync(DbConnection connection, string selectForUpdate, (string Name, string Value) parameter)
    {
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = selectForUpdate;
        var bound = command.CreateParameter();
        bound.ParameterName = parameter.Name;
        bound.Value = parameter.Value;
        command.Parameters.Add(bound);

        Assert.NotNull(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        return new RowLock(connection, transaction);
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
