using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MySqlConnector;

namespace AxisRepository.MySql.IntegrationTests;

[Collection("AxisRepositoryMySqlCollection")]
public class MySqlRepositoryBaseTests(MySqlFixture fixture)
{
    private sealed record Row(int Id, string Name);

    private sealed class TestRepo(IAxisMediator mediator, IAxisLogger<MySqlRepositoryBase> logger, IMySqlUnitOfWork uow)
        : MySqlRepositoryBase(mediator, logger, uow)
    {
        public Task<AxisResult> InsertAsync(int id, string name, string? duplicateCode = null)
            => ExecuteAsync("INSERT INTO repo_tests.items (id, name) VALUES (@id, @name)",
                p => { p.AddWithValue("id", id); p.AddWithValue("name", name); },
                duplicateCode);

        public Task<AxisResult> UpdateNameAsync(int id, string name)
            => ExecuteAsync("UPDATE repo_tests.items SET name = @name WHERE id = @id",
                p => { p.AddWithValue("id", id); p.AddWithValue("name", name); });

        public Task<AxisResult<Row>> GetAsync(int id)
            => GetAsync("SELECT id, name FROM repo_tests.items WHERE id = @id",
                p => p.AddWithValue("id", id),
                r => new Row(r.GetInt32(0), r.GetString(1)),
                "ROW_NOT_FOUND");

        public Task<AxisResult<IEnumerable<Row>>> ListAsync()
            => ListAsync("SELECT id, name FROM repo_tests.items ORDER BY id",
                _ => { },
                r => new Row(r.GetInt32(0), r.GetString(1)));

        // Unknown column / column-count mismatch: REAL failures against an existing table, so they
        // must keep mapping to the generic error — unlike the missing-relation cases below.
        public Task<AxisResult> InvalidSqlAsync()
            => ExecuteAsync("INSERT INTO repo_tests.items (id) VALUES (@id, @extra)",
                p => { p.AddWithValue("id", 1); p.AddWithValue("extra", 2); });

        public Task<AxisResult<Row>> InvalidSelectAsync()
            => GetAsync("SELECT no_such_column FROM repo_tests.items",
                _ => { },
                r => new Row(r.GetInt32(0), r.GetString(1)),
                "NOT_FOUND");

        public Task<AxisResult<IEnumerable<Row>>> InvalidListAsync()
            => ListAsync("SELECT no_such_column FROM repo_tests.items",
                _ => { },
                r => new Row(r.GetInt32(0), r.GetString(1)));

        // Pre-migrations shape: relation that does not exist yet (1146 no such table, also raised
        // for a table qualified by a database that does not exist).
        public Task<AxisResult> ExecuteOnMissingTableAsync()
            => ExecuteAsync("INSERT INTO repo_tests.missing_items (id) VALUES (@id)",
                p => p.AddWithValue("id", 1));

        public Task<AxisResult<Row>> GetFromMissingSchemaAsync()
            => GetAsync("SELECT id, name FROM no_such_schema.items WHERE id = @id",
                p => p.AddWithValue("id", 1),
                r => new Row(r.GetInt32(0), r.GetString(1)),
                "NOT_FOUND");

        public Task<AxisResult<IEnumerable<Row>>> ListFromMissingTableAsync()
            => ListAsync("SELECT id, name FROM repo_tests.missing_items",
                _ => { },
                r => new Row(r.GetInt32(0), r.GetString(1)));

        // Provider-typed surface overloads that take no explicit parameters / the typed ExecuteCount.
        public Task<AxisResult> DeleteNothingAsync()
            => ExecuteAsync("DELETE FROM repo_tests.items WHERE id < 0");

        public Task<AxisResult<int>> TouchAsync(int id)
            => ExecuteCountAsync("UPDATE repo_tests.items SET name = name WHERE id = @id",
                p => p.AddWithValue("id", id));

        public Task<AxisResult<Row>> GetFirstAsync()
            => GetAsync("SELECT id, name FROM repo_tests.items ORDER BY id LIMIT 1",
                r => new Row(r.GetInt32(0), r.GetString(1)), "NONE");

        public Task<AxisResult<IEnumerable<Row>>> ListAllAsync()
            => ListAsync("SELECT id, name FROM repo_tests.items",
                r => new Row(r.GetInt32(0), r.GetString(1)));
    }

    private (TestRepo Repo, MySqlUnitOfWork Uow) Build()
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

        var uow = new MySqlUnitOfWork(
            mediator.Object,
            dataSource,
            NullAxisTelemetry.Instance,
            provider.GetRequiredService<IAxisLogger<MySqlUnitOfWork>>(),
            new NullAxisRepositoryOutbox());

        var repo = new TestRepo(
            mediator.Object,
            provider.GetRequiredService<IAxisLogger<MySqlRepositoryBase>>(),
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

        var mockUow = new Mock<IMySqlUnitOfWork>();
        mockUow.Setup(x => x.NewCommandAsync(It.IsAny<string>())).ThrowsAsync(exception);

        return new TestRepo(
            mediator.Object,
            provider.GetRequiredService<IAxisLogger<MySqlRepositoryBase>>(),
            mockUow.Object);
    }

    // MySQL DDL implicitly commits, so the schema/table are created on a plain autocommit connection
    // rather than inside a unit-of-work transaction (which the DDL would otherwise silently end).
    private async Task EnsureTableAsync()
    {
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using (var s = new MySqlCommand("CREATE SCHEMA IF NOT EXISTS repo_tests", conn))
            await s.ExecuteNonQueryAsync();
        await using var t = new MySqlCommand(
            "CREATE TABLE IF NOT EXISTS repo_tests.items (id INT PRIMARY KEY, name VARCHAR(50))", conn);
        await t.ExecuteNonQueryAsync();
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
        await using var badDataSource = new MySqlDataSource("Server=127.0.0.1;Port=1;Uid=x;Pwd=x;Database=x;Connection Timeout=3");
        await using var uow = new MySqlUnitOfWork(
            mediator.Object, badDataSource, NullAxisTelemetry.Instance, provider.GetRequiredService<IAxisLogger<MySqlUnitOfWork>>(), new NullAxisRepositoryOutbox());
        var repo = new TestRepo(mediator.Object, provider.GetRequiredService<IAxisLogger<MySqlRepositoryBase>>(), uow);

        var result = await repo.InsertAsync(1, "x");

        result.ShouldFailWithCode("MYSQL_EXECUTION_ERROR");
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
                r => new Row(r.GetInt32(0), r.GetString(1)), "NOT_FOUND");
            got.ShouldSucceed();
            Assert.Equal("binder", got.Value.Name);
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
            got.ShouldSucceed();
            Assert.Equal("one", got.Value.Name);
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
            list.ShouldSucceed();
            Assert.True(list.Value.Count() >= 2);
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
    public async Task ExecuteCountReturnsAffectedRowCount()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        IAxisDbRepository db = repo;
        using (uow)
        {
            var inserted = await db.ExecuteCountAsync(
                "INSERT INTO repo_tests.items (id, name) VALUES (@id, @name)",
                b => b.Add("id", 7100).Add("name", "x"));
            inserted.ShouldSucceedWith(1);
            await uow.SaveChangesAsync();

            var updated = await db.ExecuteCountAsync(
                "UPDATE repo_tests.items SET name = @n WHERE id = @id",
                b => b.Add("n", "y").Add("id", 7100));
            updated.ShouldSucceedWith(1);
            await uow.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ExecuteCountReturnsConflictOnDuplicateKey()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            IAxisDbRepository db = repo;
            await db.ExecuteCountAsync("INSERT INTO repo_tests.items (id, name) VALUES (@id, @name)",
                b => b.Add("id", 7200).Add("name", "x"));
            await uow.SaveChangesAsync();
        }

        var (repo2, uow2) = Build();
        using (uow2)
        {
            IAxisDbRepository db2 = repo2;
            var dup = await db2.ExecuteCountAsync("INSERT INTO repo_tests.items (id, name) VALUES (@id, @name)",
                b => b.Add("id", 7200).Add("name", "x"), "DUP_COUNT");
            dup.ShouldFailWithCode("DUP_COUNT");
        }
    }

    [Fact]
    public async Task BinderBindsJsonAndNullValues()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        IAxisDbRepository db = repo;
        using (uow)
        {
            var withJson = await db.ExecuteAsync(
                "INSERT INTO repo_tests.items (id, name) VALUES (@id, @name)",
                b => b.Add("id", 7400).AddJson("name", "jsonish"));
            withJson.ShouldSucceed();
            await uow.SaveChangesAsync();

            var got = await db.GetAsync(
                "SELECT id, name FROM repo_tests.items WHERE id = @id",
                b => b.Add("id", 7400),
                r => r.GetString(1), "NF");
            got.ShouldSucceedWith("jsonish");

            // A null binder value becomes DBNull (matches nothing); exercises the null branch without
            // leaving a NULL row that a sibling test's reader would choke on.
            var none = await db.GetAsync(
                "SELECT id, name FROM repo_tests.items WHERE name = @name",
                b => b.Add("name", null),
                r => r.GetString(1), "NONE_FOUND");
            none.ShouldFailWithCode("NONE_FOUND");
        }
    }

    [Fact]
    public async Task ProviderTypedNoParamOverloadsWork()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            await repo.InsertAsync(8000, "first");
            await uow.SaveChangesAsync();

            await repo.DeleteNothingAsync().ShouldSucceedAsync();   // no-param ExecuteAsync(sql, code)

            var touched = await repo.TouchAsync(8000);                 // provider-typed ExecuteCountAsync
            touched.ShouldSucceedWith(1);

            await repo.GetFirstAsync().ShouldSucceedAsync();       // no-param GetAsync
            Assert.True((await repo.ListAllAsync().ShouldSucceedAsync()).Any());      // no-param ListAsync
            await uow.SaveChangesAsync();
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

            result.ShouldFailWithCode("MYSQL_EXECUTION_ERROR");
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

            result.ShouldFailWithCode("MYSQL_GET_ERROR");
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

            result.ShouldFailWithCode("MYSQL_LIST_ERROR");
        }
    }

    // ── Missing relation before migrations (1146) vs real error ──────────────

    [Fact]
    public async Task ExecuteAsyncReturnsSchemaNotReadyWhenTableMissing()
    {
        await EnsureTableAsync();
        var (repo, uow) = Build();
        using (uow)
        {
            var result = await repo.ExecuteOnMissingTableAsync();

            result.ShouldFail();
            var error = Assert.Single(result.Errors);
            Assert.Equal("MYSQL_SCHEMA_NOT_READY", error.Code);
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

            result.ShouldFail();
            var error = Assert.Single(result.Errors);
            Assert.Equal("MYSQL_SCHEMA_NOT_READY", error.Code);
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

            result.ShouldFail();
            var error = Assert.Single(result.Errors);
            Assert.Equal("MYSQL_SCHEMA_NOT_READY", error.Code);
            Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenFatalExceptionOccurs_PropagatesException()
    {
        var repo = BuildWithThrowingUow(new InvalidOperationException("fatal"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.InsertAsync(9001, "fatal"));
    }

    [Fact]
    public async Task GetAsync_WhenFatalExceptionOccurs_PropagatesException()
    {
        var repo = BuildWithThrowingUow(new InvalidOperationException("fatal"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.GetAsync(9001));
    }

    [Fact]
    public async Task ListAsync_WhenFatalExceptionOccurs_PropagatesException()
    {
        var repo = BuildWithThrowingUow(new InvalidOperationException("fatal"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var repo = BuildWithThrowingUow(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => repo.InsertAsync(9001, "cancelled"));
    }

    [Fact]
    public async Task GetAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var repo = BuildWithThrowingUow(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => repo.GetAsync(9001));
    }

    [Fact]
    public async Task ListAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var repo = BuildWithThrowingUow(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => repo.ListAsync());
    }

    // ── Deadlock (1213) after a write surfaces as a typed transient ──────────
    // Two transactions lock two rows in opposite order and then cross, forcing InnoDB to abort one as the
    // deadlock victim. The victim hits the transient AFTER a write already landed in its transaction (the
    // ConsumeSeat-under-contention shape), so the base must surface it as ServiceUnavailable/IsTransient —
    // not a generic 500 — the exact failure the perf.sh MySQL run exposed.
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
        Assert.Equal("MYSQL_TRANSIENT_ERROR", error.Code);
        Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type);
        Assert.True(error.IsTransient);
    }
}
