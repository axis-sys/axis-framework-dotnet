using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;

namespace AxisRepository.Postgres.IntegrationTests;

[Collection("AxisRepositoryPostgresCollection")]
public class PostgresRepositoryBaseTests(PostgresFixture fixture)
{
    private sealed record Row(int Id, string Name);

    private sealed class TestRepo(IAxisMediator mediator, IAxisLogger<PostgresRepositoryBase> logger, IPostgresUnitOfWork uow)
        : PostgresRepositoryBase(mediator, logger, uow)
    {
        public Task<AxisResult> InsertAsync(int id, string name, string? duplicateCode = null)
            => ExecuteAsync("INSERT INTO repo_tests.items (id, name) VALUES ($1, $2)",
                p => { p.AddWithValue(id); p.AddWithValue(name); },
                duplicateCode);

        public Task<AxisResult> UpdateNameAsync(int id, string name)
            => ExecuteAsync("UPDATE repo_tests.items SET name = $2 WHERE id = $1",
                p => { p.AddWithValue(id); p.AddWithValue(name); });

        public Task<AxisResult<Row>> GetAsync(int id)
            => GetAsync("SELECT id, name FROM repo_tests.items WHERE id = $1",
                p => p.AddWithValue(id),
                r => new Row(r.GetInt32(0), r.GetString(1)),
                "ROW_NOT_FOUND");

        public Task<AxisResult<IEnumerable<Row>>> ListAsync()
            => ListAsync("SELECT id, name FROM repo_tests.items ORDER BY id",
                _ => { },
                r => new Row(r.GetInt32(0), r.GetString(1)));

        // Division by zero: a REAL execution failure against an existing table, so it must keep
        // mapping to the generic error — unlike the missing-relation cases below.
        public Task<AxisResult> InvalidSqlAsync()
            => ExecuteAsync("INSERT INTO repo_tests.items (id, name) VALUES (1/0, 'boom')",
                _ => { });

        public Task<AxisResult<Row>> InvalidSelectAsync()
            => GetAsync("SELECT 1/0, 'boom'",
                _ => { },
                r => new Row(r.GetInt32(0), r.GetString(1)),
                "NOT_FOUND");

        public Task<AxisResult<IEnumerable<Row>>> InvalidListAsync()
            => ListAsync("SELECT 1/0, 'boom'",
                _ => { },
                r => new Row(r.GetInt32(0), r.GetString(1)));

        // Pre-migrations shape: schema-qualified relation that does not exist (42P01).
        public Task<AxisResult> ExecuteOnMissingTableAsync()
            => ExecuteAsync("INSERT INTO repo_tests.missing_items (id) VALUES ($1)",
                p => p.AddWithValue(1));

        public Task<AxisResult<Row>> GetFromMissingSchemaAsync()
            => GetAsync("SELECT id, name FROM no_such_schema.items WHERE id = $1",
                p => p.AddWithValue(1),
                r => new Row(r.GetInt32(0), r.GetString(1)),
                "NOT_FOUND");

        public Task<AxisResult<IEnumerable<Row>>> ListFromMissingTableAsync()
            => ListAsync("SELECT id, name FROM repo_tests.missing_items",
                _ => { },
                r => new Row(r.GetInt32(0), r.GetString(1)));
    }

    private (TestRepo Repo, PostgresUnitOfWork Uow) Build()
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

        var uow = new PostgresUnitOfWork(
            mediator.Object,
            dataSource,
            NullAxisTelemetry.Instance,
            provider.GetRequiredService<IAxisLogger<PostgresUnitOfWork>>(),
            new NullAxisRepositoryOutbox());

        var repo = new TestRepo(
            mediator.Object,
            provider.GetRequiredService<IAxisLogger<PostgresRepositoryBase>>(),
            uow);

        return (repo, uow);
    }

    private TestRepo BuildWithThrowingUow(Exception exception)
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
        var provider = services.BuildServiceProvider();

        var mockUow = new Mock<IPostgresUnitOfWork>();
        mockUow.Setup(x => x.NewCommandAsync(It.IsAny<string>())).ThrowsAsync(exception);

        return new TestRepo(
            mediator.Object,
            provider.GetRequiredService<IAxisLogger<PostgresRepositoryBase>>(),
            mockUow.Object);
    }

    private async Task EnsureTableAsync()
    {
        var (_, uow) = Build();
        using (uow)
        {
            await using var s = await uow.NewCommandAsync("CREATE SCHEMA IF NOT EXISTS repo_tests");
            await s.ExecuteNonQueryAsync();
            await using var t = await uow.NewCommandAsync(
                "CREATE TABLE IF NOT EXISTS repo_tests.items (id INT PRIMARY KEY, name VARCHAR(50))");
            await t.ExecuteNonQueryAsync();
            await uow.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task FailedConnectionStartIsMappedToErrorNotThrown()
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
        var provider = services.BuildServiceProvider();

        // An unreachable database: StartAsync fails (caught into a failed AxisResult), and NewCommandAsync
        // surfaces it as an AxisDbException the base maps to a clean error — it must NOT escape as a throw.
        await using var badDataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=3");
        await using var uow = new PostgresUnitOfWork(
            mediator.Object, badDataSource, NullAxisTelemetry.Instance, provider.GetRequiredService<IAxisLogger<PostgresUnitOfWork>>(), new NullAxisRepositoryOutbox());
        var repo = new TestRepo(mediator.Object, provider.GetRequiredService<IAxisLogger<PostgresRepositoryBase>>(), uow);

        var result = await repo.InsertAsync(1, "x");

        result.ShouldFailWithCode("POSTGRES_EXECUTION_ERROR");
    }

    [Fact]
    public async Task BinderSurfaceInsertsAndReadsBackWithNamedParams()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        IAxisDbRepository db = repo;
        using (uow)
        {
            var insert = await db.ExecuteAsync(
                "INSERT INTO repo_tests.items (id, name) VALUES (@id, @name)",
                b => b.Add("id", 7000).Add("name", "binder"), "DUP");
            insert.ShouldSucceed();
            await uow.SaveChangesAsync();

            var got = await db.GetAsync(
                "SELECT id, name FROM repo_tests.items WHERE id = @id",
                b => b.Add("id", 7000),
                r => new Row(r.GetInt32(0), r.GetString(1)),
                "NOT_FOUND");
            Assert.Equal("binder", got.ShouldSucceed().Name);
        }
    }

    [Fact]
    public async Task ExecuteAsyncInsertsAndReadsBack()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            var insert = await repo.InsertAsync(1, "one");
            insert.ShouldSucceed();
            await uow.SaveChangesAsync();

            var got = await repo.GetAsync(1);
            Assert.Equal("one", got.ShouldSucceed().Name);
        }
    }

    [Fact]
    public async Task GetAsyncReturnsNotFoundWhenRowMissing()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            var got = await repo.GetAsync(99999);

            got.ShouldFailWithCode("ROW_NOT_FOUND");
        }
    }

    [Fact]
    public async Task ListAsyncReturnsAllRows()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            await repo.InsertAsync(101, "a");
            await repo.InsertAsync(102, "b");
            await uow.SaveChangesAsync();

            var list = await repo.ListAsync();
            Assert.True(list.ShouldSucceed().Count() >= 2);
        }
    }

    [Fact]
    public async Task ExecuteAsyncReturnsConflictOnDuplicateKey()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            await repo.InsertAsync(500, "first");
            await uow.SaveChangesAsync();
        }

        var (repo2, uow2) = Build();
        using (uow2)
        {
            var dup = await repo2.InsertAsync(500, "second", "ITEM_ALREADY_EXISTS");
            dup.ShouldFailWithCode("ITEM_ALREADY_EXISTS");
        }
    }

    [Fact]
    public async Task ExecuteAsyncReturnsGenericErrorForNonDuplicateFailure()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            var result = await repo.InvalidSqlAsync();

            result.ShouldFailWithCode("POSTGRES_EXECUTION_ERROR");
        }
    }

    [Fact]
    public async Task GetAsyncReturnsErrorWhenSqlInvalid()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            var result = await repo.InvalidSelectAsync();

            result.ShouldFailWithCode("POSTGRES_GET_ERROR");
        }
    }

    [Fact]
    public async Task ListAsyncReturnsErrorWhenSqlInvalid()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            var result = await repo.InvalidListAsync();

            result.ShouldFailWithCode("POSTGRES_LIST_ERROR");
        }
    }

    // ── Missing relation before migrations (42P01) vs real error ─────────────

    [Fact]
    public async Task ExecuteAsyncReturnsSchemaNotReadyWhenTableMissing()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            var result = await repo.ExecuteOnMissingTableAsync();

            var error = Assert.Single(result.ShouldFail());
            Assert.Equal("POSTGRES_SCHEMA_NOT_READY", error.Code);
            Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
            Assert.True(error.IsTransient);
        }
    }

    [Fact]
    public async Task GetAsyncReturnsSchemaNotReadyWhenSchemaMissing()
    {
        var (repo, uow) = Build();
        using (uow)
        {
            var result = await repo.GetFromMissingSchemaAsync();

            var error = Assert.Single(result.ShouldFail());
            Assert.Equal("POSTGRES_SCHEMA_NOT_READY", error.Code);
            Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
        }
    }

    [Fact]
    public async Task ListAsyncReturnsSchemaNotReadyWhenTableMissing()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            var result = await repo.ListFromMissingTableAsync();

            var error = Assert.Single(result.ShouldFail());
            Assert.Equal("POSTGRES_SCHEMA_NOT_READY", error.Code);
            Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
        }
    }

    // ── Fatal exception propagation (STORY-0A.2.3) ───────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenFatalExceptionOccurs_PropagatesException()
    {
        var repo = BuildWithThrowingUow(new InvalidOperationException("fatal"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.InsertAsync(9001, "fatal"));
    }

    [Fact]
    public async Task GetAsync_WhenFatalExceptionOccurs_PropagatesException()
    {
        var repo = BuildWithThrowingUow(new InvalidOperationException("fatal"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.GetAsync(9001));
    }

    [Fact]
    public async Task ListAsync_WhenFatalExceptionOccurs_PropagatesException()
    {
        var repo = BuildWithThrowingUow(new InvalidOperationException("fatal"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.ListAsync());
    }

    // ── OperationCanceledException propagation (STORY-0A.2.2) ────────────────

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var repo = BuildWithThrowingUow(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.InsertAsync(9001, "cancelled"));
    }

    [Fact]
    public async Task GetAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var repo = BuildWithThrowingUow(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.GetAsync(9001));
    }

    [Fact]
    public async Task ListAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var repo = BuildWithThrowingUow(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.ListAsync());
    }

    // ── Deadlock (40P01) after a write surfaces as a typed transient ─────────
    // Two transactions lock two rows in opposite order and then cross, forcing Postgres to abort one as the
    // deadlock victim. The victim hits the transient AFTER a write already landed in its transaction (the
    // ConsumeSeat-under-contention shape), so the base must surface it as ServiceUnavailable/IsTransient —
    // not a generic 500 — proving Postgres parity with the MySQL deadlock path.
    [Fact]
    public async Task DeadlockAfterWriteSurfacesAsTypedTransientError()
    {
        await EnsureTableAsync();
        var (seed, seedUow) = Build();
        using (seedUow)
        {
            await seed.InsertAsync(9100, "a");
            await seed.InsertAsync(9200, "b");
            await seedUow.SaveChangesAsync();
        }

        using var bothLocked = new SemaphoreSlim(0, 2);
        using var cross = new SemaphoreSlim(0, 2);

        async Task<AxisResult> CrossUpdateAsync(int firstId, int secondId)
        {
            var (repo, uow) = Build();
            await using (uow)
            {
                await repo.UpdateNameAsync(firstId, "x");   // hold the lock on firstId (a durable write lands)
                // ReSharper disable once AccessToDisposedClosure -- awaited via Task.WhenAll before the outer `using` disposes it
                bothLocked.Release();
                // ReSharper disable once AccessToDisposedClosure -- awaited via Task.WhenAll before the outer `using` disposes it
                await cross.WaitAsync();
                var second = await repo.UpdateNameAsync(secondId, "y");   // cross-lock → one side deadlocks
                if (second.IsSuccess)
                    await uow.SaveChangesAsync();
                return second;
            }
        }

        var t1 = CrossUpdateAsync(9100, 9200);
        var t2 = CrossUpdateAsync(9200, 9100);
        await bothLocked.WaitAsync(TestContext.Current.CancellationToken);
        await bothLocked.WaitAsync(TestContext.Current.CancellationToken);
        cross.Release(2);
        var results = await Task.WhenAll(t1, t2);

        var victim = Assert.Single(results, r => r.IsFailure);
        var error = Assert.Single(victim.Errors);
        Assert.Equal("POSTGRES_TRANSIENT_ERROR", error.Code);
        Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
        Assert.True(error.IsTransient);
    }
}
