using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MySqlConnector;

namespace AxisRepository.MySql.IntegrationTests;

[Collection("AxisRepositoryMySqlCollection")]
public class MySqlUnitOfWorkTests(MySqlFixture fixture)
{
    private MySqlUnitOfWork BuildUow()
    {
        var services = new ServiceCollection();
        services.AddAxisLogger();
        var mediator = new Mock<IAxisMediator>();
        mediator.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        mediator.SetupGet(x => x.TraceId).Returns("trace");
        mediator.SetupGet(x => x.OriginId).Returns("origin");
        mediator.SetupGet(x => x.JourneyId).Returns((string?)null);
        services.AddSingleton(mediator.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);

        var dataSource = new MySqlDataSource(fixture.ConnectionString);
        services.AddSingleton(dataSource);
        var provider = services.BuildServiceProvider();

        return new MySqlUnitOfWork(
            mediator.Object,
            dataSource,
            NullAxisTelemetry.Instance,
            provider.GetRequiredService<IAxisLogger<MySqlUnitOfWork>>(),
            new NullAxisRepositoryOutbox());
    }

    [Fact]
    public async Task StartAsyncOpensConnectionAndTransaction()
    {
        using var uow = BuildUow();

        var result = await uow.StartAsync();

        result.ShouldSucceed();
    }

    [Fact]
    public async Task SaveChangesAsyncReturnsErrorWhenTransactionNotStarted()
    {
        using var uow = BuildUow();

        var result = await uow.SaveChangesAsync();

        result.ShouldFailWithCode("MYSQL_TRANSACTION_NOT_STARTED");
    }

    [Fact]
    public async Task CommitRoundtripsInsertedRow()
    {
        await EnsureSandboxTableAsync();
        using var uow = BuildUow();

        await using (var cmd = await uow.NewCommandAsync("INSERT INTO sandbox.tests (id, name) VALUES (@id, @name)"))
        {
            cmd.Parameters.AddWithValue("id", 1);
            cmd.Parameters.AddWithValue("name", "hello");
            await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var saved = await uow.SaveChangesAsync();
        saved.ShouldSucceed();

        using var uow2 = BuildUow();
        await using var verify = await uow2.NewCommandAsync("SELECT name FROM sandbox.tests WHERE id = 1");
        await using var reader = await verify.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("hello", reader.GetString(0));
    }

    [Fact]
    public async Task RollbackAsyncDiscardsChanges()
    {
        await EnsureSandboxTableAsync();
        using var uow = BuildUow();

        await using (var cmd = await uow.NewCommandAsync("INSERT INTO sandbox.tests (id, name) VALUES (@id, @name)"))
        {
            cmd.Parameters.AddWithValue("id", 99);
            cmd.Parameters.AddWithValue("name", "rollback");
            await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var rolledBack = await uow.RollbackAsync();
        rolledBack.ShouldSucceed();

        using var uow2 = BuildUow();
        await using var verify = await uow2.NewCommandAsync("SELECT COUNT(*) FROM sandbox.tests WHERE id = 99");
        await using var reader = await verify.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0L, reader.GetInt64(0));
    }

    [Fact]
    public async Task RollbackAsyncSucceedsWithoutOpenTransaction()
    {
        using var uow = BuildUow();

        var result = await uow.RollbackAsync();

        result.ShouldSucceed();
    }

    [Fact]
    public async Task DisposeAsyncDisposesConnection()
    {
        var uow = BuildUow();
        await uow.StartAsync();

        await uow.DisposeAsync();
    }

    [Fact]
    public void DisposeDoesNotThrowBeforeStart()
    {
        var uow = BuildUow();

        uow.Dispose();
    }

    [Fact]
    public async Task StartAsyncReturnsErrorForInvalidConnectionString()
    {
        var services = new ServiceCollection();
        services.AddAxisLogger();
        var mediator = new Mock<IAxisMediator>();
        mediator.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        mediator.SetupGet(x => x.TraceId).Returns("t");
        mediator.SetupGet(x => x.OriginId).Returns((string?)null);
        mediator.SetupGet(x => x.JourneyId).Returns((string?)null);
        services.AddSingleton(mediator.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
        var provider = services.BuildServiceProvider();

        var dataSource = new MySqlDataSource("Server=localhost;Port=1;Database=x;User ID=x;Password=x;Connection Timeout=1");
        using var uow = new MySqlUnitOfWork(
            mediator.Object,
            dataSource,
            NullAxisTelemetry.Instance,
            provider.GetRequiredService<IAxisLogger<MySqlUnitOfWork>>(),
            new NullAxisRepositoryOutbox());

        var result = await uow.StartAsync();

        result.ShouldFailWithCode("MYSQL_ERROR_STARTING_CONNECTION");
    }

    [Fact]
    public async Task SaveChangesAsyncReturnsErrorAfterCommitWhenTransactionCleared()
    {
        using var uow = BuildUow();
        await uow.StartAsync();

        await uow.SaveChangesAsync();

        // After the first commit the transaction is cleared, so a second SaveChanges reports NOT_STARTED.
        var result = await uow.SaveChangesAsync();
        result.ShouldFailWithCode("MYSQL_TRANSACTION_NOT_STARTED");
    }

    [Fact]
    public async Task RollbackAsyncReturnsErrorWhenTransactionAlreadyCompleted()
    {
        using var uow = BuildUow();
        await uow.StartAsync();

        var first = await uow.RollbackAsync();
        first.ShouldSucceed();

        // Second rollback hits the already-completed transaction → rollback failure.
        var second = await uow.RollbackAsync();
        second.ShouldFailWithCode("MYSQL_ROLLBACK_ERROR");
    }

    [Fact]
    public async Task SaveChangesAsyncReturnsErrorWhenCommitThrows()
    {
        var uow = BuildUow();
        await uow.StartAsync();

        // Dispose the connection (which backs the transaction) so CommitAsync fails.
        await uow.DisposeAsync();

        var result = await uow.SaveChangesAsync();

        result.ShouldFailWithCode("MYSQL_SAVING_CHANGES_ERROR");
    }

    // MySQL DDL implicitly commits, so create the sandbox on a plain autocommit connection.
    private async Task EnsureSandboxTableAsync()
    {
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using (var schemaCmd = new MySqlCommand("CREATE SCHEMA IF NOT EXISTS sandbox", conn))
            await schemaCmd.ExecuteNonQueryAsync();
        await using var tableCmd = new MySqlCommand(
            "CREATE TABLE IF NOT EXISTS sandbox.tests (id INT PRIMARY KEY, name VARCHAR(50))", conn);
        await tableCmd.ExecuteNonQueryAsync();
    }
}
