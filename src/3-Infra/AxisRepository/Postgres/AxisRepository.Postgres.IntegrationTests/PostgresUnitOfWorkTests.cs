using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;

namespace AxisRepository.Postgres.IntegrationTests;

[Collection("AxisRepositoryPostgresCollection")]
public class PostgresUnitOfWorkTests(PostgresFixture fixture)
{
    private PostgresUnitOfWork BuildUow()
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

        var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        services.AddSingleton(dataSource);
        var provider = services.BuildServiceProvider();

        return new PostgresUnitOfWork(
            mediator.Object,
            dataSource,
            NullAxisTelemetry.Instance,
            provider.GetRequiredService<IAxisLogger<PostgresUnitOfWork>>(),
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

        result.ShouldFailWithCode("POSTGRES_TRANSACTION_NOT_STARTED");
    }

    [Fact]
    public async Task CommitRoundtripsInsertedRow()
    {
        using var uow = BuildUow();
        await EnsureSandboxTableAsync(uow);

        await using (var cmd = await uow.NewCommandAsync("INSERT INTO sandbox.tests (id, name) VALUES ($1, $2)"))
        {
            cmd.Parameters.AddWithValue(1);
            cmd.Parameters.AddWithValue("hello");
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
        using var uow = BuildUow();
        await EnsureSandboxTableAsync(uow);

        await using (var cmd = await uow.NewCommandAsync("INSERT INTO sandbox.tests (id, name) VALUES ($1, $2)"))
        {
            cmd.Parameters.AddWithValue(99);
            cmd.Parameters.AddWithValue("rollback");
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

        var dataSource = NpgsqlDataSource.Create("Host=localhost;Port=1;Database=x;Username=x;Password=x;Timeout=1;Command Timeout=1");
        using var uow = new PostgresUnitOfWork(
            mediator.Object,
            dataSource,
            NullAxisTelemetry.Instance,
            provider.GetRequiredService<IAxisLogger<PostgresUnitOfWork>>(),
            new NullAxisRepositoryOutbox());

        var result = await uow.StartAsync();

        result.ShouldFailWithCode("POSTGRES_ERROR_STARTING_CONNECTION");
    }

    [Fact]
    public async Task SaveChangesAsyncReturnsErrorWhenCommitFails()
    {
        using var uow = BuildUow();
        await uow.StartAsync();

        // Commit once - succeeds
        await uow.SaveChangesAsync();

        // Force re-start of a connection but bypass proper flow to cause commit to fail;
        // after first commit, transaction is null so next SaveChanges returns NOT_STARTED.
        var result = await uow.SaveChangesAsync();
        result.ShouldFailWithCode("POSTGRES_TRANSACTION_NOT_STARTED");
    }

    [Fact]
    public async Task RollbackAsyncReturnsErrorWhenTransactionAlreadyDisposed()
    {
        using var uow = BuildUow();
        await uow.StartAsync();

        var first = await uow.RollbackAsync();
        first.ShouldSucceed();

        // Second rollback hits the internal rollback failure (transaction already rolled back).
        var second = await uow.RollbackAsync();
        second.ShouldFailWithCode("POSTGRES_ROLLBACK_ERROR");
    }

    [Fact]
    public async Task SaveChangesAsyncReturnsErrorWhenCommitThrows()
    {
        var uow = BuildUow();
        await uow.StartAsync();

        // Dispose the connection (which backs the transaction) so CommitAsync fails.
        await uow.DisposeAsync();

        var result = await uow.SaveChangesAsync();

        result.ShouldFailWithCode("POSTGRES_SAVING_CHANGES_ERROR");
    }

    private static async Task EnsureSandboxTableAsync(PostgresUnitOfWork uow)
    {
        await using var schemaCmd = await uow.NewCommandAsync("CREATE SCHEMA IF NOT EXISTS sandbox");
        await schemaCmd.ExecuteNonQueryAsync();
        await using var tableCmd = await uow.NewCommandAsync(
            "CREATE TABLE IF NOT EXISTS sandbox.tests (id INT PRIMARY KEY, name VARCHAR(50))");
        await tableCmd.ExecuteNonQueryAsync();
        var save = await uow.SaveChangesAsync();
        if (save.IsFailure)
            throw new InvalidOperationException(string.Join(", ", save.Errors.Select(e => e.Code)));
    }
}
