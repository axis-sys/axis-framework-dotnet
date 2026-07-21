using Axis.Ddl;
using Npgsql;

namespace AxisRepository.Postgres.IntegrationTests;

// Runs DDL rendered by PostgresSqlDialect against a real Postgres and asserts BEHAVIOUR — BOOLEAN/JSONB/
// TIMESTAMPTZ types, NOW() default, standalone partial indexes (WHERE), FK constraints, and ON CONFLICT
// seeds. The dialect twin of MySqlDdlBuilderIntegrationTests, so the single AxisTable definition is proven
// to yield correct, behaviourally-equivalent schema on both engines. Each test uses its own schema.
[Collection("AxisRepositoryPostgresCollection")]
public class PostgresDdlBuilderIntegrationTests(PostgresFixture fixture)
{
    private const string DuplicateKey = "23505";
    private readonly PostgresSqlDialect _dialect = new();

    private async Task MigrateAsync(string schema, params AxisTable[] tables)
    {
        var script = string.Join("\n", tables.Select(t => t.Render(_dialect)));
        await new PostgresMigrationRunner().RunAsync(fixture.ConnectionString, schema, [("V1", script)]);
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return await cmd.ExecuteScalarAsync();
    }

    [Fact]
    public async Task TypesAndUtcDefaultRoundTrip()
    {
        const string schema = "DDLB_TYPES";
        var items = new AxisTable($"{schema}.ITEMS")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("NAME", AxisDbType.Varchar(120), notNull: true)
            .Column("NOTES", AxisDbType.Text)
            .Column("QTY", AxisDbType.Int, notNull: true)
            .Column("IS_ON", AxisDbType.Bool, notNull: true, @default: AxisDefault.Bool(true))
            .Column("META", AxisDbType.Json)
            .Column("AMOUNT", AxisDbType.Decimal(18, 4))
            .Column("CREATED_AT", AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc);

        await MigrateAsync(schema, items);

        await using var conn = await OpenAsync();
        await ExecAsync(conn,
            $"INSERT INTO {schema}.ITEMS (ID, NAME, NOTES, QTY, META, AMOUNT) " +
            "VALUES ('a', 'Acme', 'note', 5, '{\"k\":\"v\"}', 12.3456)");

        Assert.Equal("Acme", await ScalarAsync(conn, $"SELECT NAME FROM {schema}.ITEMS WHERE ID='a'"));
        Assert.Equal(true, await ScalarAsync(conn, $"SELECT IS_ON FROM {schema}.ITEMS WHERE ID='a'"));
        Assert.NotNull(await ScalarAsync(conn, $"SELECT CREATED_AT FROM {schema}.ITEMS WHERE ID='a'"));
    }

    [Fact]
    public async Task PartialUniqueAllowsRowsOutsidePredicateButBlocksInside()
    {
        const string schema = "DDLB_PUNIQ";
        var items = new AxisTable($"{schema}.ITEMS")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("TENANT", AxisDbType.Varchar(50), notNull: true)
            .Column("TITLE", AxisDbType.Varchar(120), notNull: true)
            .Column("IS_ACTIVE", AxisDbType.Bool, notNull: true)
            .PartialUnique("UX_ITEMS_TENANT_TITLE", "IS_ACTIVE = TRUE", "TENANT", "TITLE");

        await MigrateAsync(schema, items);

        await using var conn = await OpenAsync();
        // Inactive rows are outside the partial index → no collision.
        await ExecAsync(conn, $"INSERT INTO {schema}.ITEMS VALUES ('1','t','dup',FALSE)");
        await ExecAsync(conn, $"INSERT INTO {schema}.ITEMS VALUES ('2','t','dup',FALSE)");

        await ExecAsync(conn, $"INSERT INTO {schema}.ITEMS VALUES ('3','t','dup',TRUE)");
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecAsync(conn, $"INSERT INTO {schema}.ITEMS VALUES ('4','t','dup',TRUE)"));
        Assert.Equal(DuplicateKey, ex.SqlState);
    }

    [Fact]
    public async Task PlainUniqueIsEnforced()
    {
        const string schema = "DDLB_UNIQ";
        var items = new AxisTable($"{schema}.ITEMS")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("CODE", AxisDbType.Varchar(60), notNull: true)
            .Unique("UX_ITEMS_CODE", "CODE");

        await MigrateAsync(schema, items);

        await using var conn = await OpenAsync();
        await ExecAsync(conn, $"INSERT INTO {schema}.ITEMS VALUES ('1','X')");
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecAsync(conn, $"INSERT INTO {schema}.ITEMS VALUES ('2','X')"));
        Assert.Equal(DuplicateKey, ex.SqlState);
    }

    [Fact]
    public async Task ForeignKeyCascadeDeletesChildren()
    {
        const string schema = "DDLB_FK";
        var parent = new AxisTable($"{schema}.PARENT")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true);
        var child = new AxisTable($"{schema}.CHILD")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("PARENT_ID", AxisDbType.Varchar(50), notNull: true)
            .ForeignKey("FK_CHILD_PARENT", "PARENT_ID", $"{schema}.PARENT", "ID", onDeleteCascade: true);

        await MigrateAsync(schema, parent, child);

        await using var conn = await OpenAsync();
        await ExecAsync(conn, $"INSERT INTO {schema}.PARENT VALUES ('p')");
        await ExecAsync(conn, $"INSERT INTO {schema}.CHILD VALUES ('c', 'p')");
        await ExecAsync(conn, $"DELETE FROM {schema}.PARENT WHERE ID='p'");

        Assert.Equal(0L, await ScalarAsync(conn, $"SELECT COUNT(*) FROM {schema}.CHILD"));
    }

    [Fact]
    public async Task SeedIsAppliedOnceAndIsIdempotent()
    {
        const string schema = "DDLB_SEED";
        var settings = new AxisTable($"{schema}.SETTINGS")
            .Column("ONLY_ROW", AxisDbType.Bool, primaryKey: true, @default: AxisDefault.Bool(true), check: AxisCheck.IsTrue)
            .Column("MAX_CONCURRENT", AxisDbType.Int, notNull: true)
            .WithSeed(["ONLY_ROW", "MAX_CONCURRENT"], ["ONLY_ROW"], new object?[] { true, 20 });

        await MigrateAsync(schema, settings);
        await MigrateAsync(schema, settings);

        await using var conn = await OpenAsync();
        Assert.Equal(1L, await ScalarAsync(conn, $"SELECT COUNT(*) FROM {schema}.SETTINGS"));
        Assert.Equal(20, await ScalarAsync(conn, $"SELECT MAX_CONCURRENT FROM {schema}.SETTINGS"));
    }
}
