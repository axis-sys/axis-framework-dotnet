using Axis.Ddl;
using MySqlConnector;

namespace AxisRepository.MySql.IntegrationTests;

// Runs DDL rendered by MySqlSqlDialect against a real MySQL and asserts BEHAVIOUR — the surest validation of
// the dialect's translations (TINYINT/JSON/DATETIME(6), UTC_TIMESTAMP default, the partial-UNIQUE generated
// column, case+accent-sensitive collation, FK constraints, idempotent seeds). Each test renders into its own
// schema so the cases are fully isolated on the shared container.
[Collection("AxisRepositoryMySqlCollection")]
public class MySqlDdlBuilderIntegrationTests(MySqlFixture fixture)
{
    private readonly MySqlSqlDialect _dialect = new();

    private async Task MigrateAsync(string schema, params AxisTable[] tables)
    {
        var script = string.Join("\n", tables.Select(t => t.Render(_dialect)));
        await new MySqlMigrationRunner().RunAsync(fixture.ConnectionString, schema, [("V1", script)]);
    }

    private async Task<MySqlConnection> OpenAsync()
    {
        var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static async Task ExecAsync(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        return await cmd.ExecuteScalarAsync();
    }

    [Fact]
    public async Task TypesAndUtcDefaultRoundTrip()
    {
        const string schema = "ddlb_types";
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
            "VALUES ('a', 'Acme', 'note', 5, JSON_OBJECT('k','v'), 12.3456)");

        Assert.Equal("Acme", await ScalarAsync(conn, $"SELECT NAME FROM {schema}.ITEMS WHERE ID='a'"));
        // IS_ON defaulted to 1, CREATED_AT defaulted to a non-null UTC timestamp.
        Assert.Equal(1, Convert.ToInt32(await ScalarAsync(conn, $"SELECT IS_ON FROM {schema}.ITEMS WHERE ID='a'")));
        Assert.NotNull(await ScalarAsync(conn, $"SELECT CREATED_AT FROM {schema}.ITEMS WHERE ID='a'"));
    }

    [Fact]
    public async Task PartialUniqueAllowsRowsOutsidePredicateButBlocksInside()
    {
        const string schema = "ddlb_puniq";
        var items = new AxisTable($"{schema}.ITEMS")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("TENANT", AxisDbType.Varchar(50), notNull: true)
            .Column("TITLE", AxisDbType.Varchar(120), notNull: true)
            .Column("IS_ACTIVE", AxisDbType.Bool, notNull: true)
            .PartialUnique("UX_ITEMS_TENANT_TITLE", "IS_ACTIVE = TRUE", "TENANT", "TITLE");

        await MigrateAsync(schema, items);

        await using var conn = await OpenAsync();
        // The generated key column is computed, never inserted, so name the real columns explicitly.
        const string cols = "(ID, TENANT, TITLE, IS_ACTIVE)";
        // Two inactive rows with the same (TENANT, TITLE): the generated key is NULL for both → no collision.
        await ExecAsync(conn, $"INSERT INTO {schema}.ITEMS {cols} VALUES ('1','t','dup',0)");
        await ExecAsync(conn, $"INSERT INTO {schema}.ITEMS {cols} VALUES ('2','t','dup',0)");

        // First active row is fine; a second active row with the same (TENANT, TITLE) must collide.
        await ExecAsync(conn, $"INSERT INTO {schema}.ITEMS {cols} VALUES ('3','t','dup',1)");
        var ex = await Assert.ThrowsAsync<MySqlException>(() =>
            ExecAsync(conn, $"INSERT INTO {schema}.ITEMS {cols} VALUES ('4','t','dup',1)"));
        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaseAndAccentSensitiveCollationTreatsCaseAsDistinct()
    {
        const string schema = "ddlb_coll";
        var items = new AxisTable($"{schema}.ITEMS")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("CODE", AxisDbType.Varchar(60), notNull: true, collation: AxisCollation.CaseAccentSensitive)
            .Unique("UX_ITEMS_CODE", "CODE");

        await MigrateAsync(schema, items);

        await using var conn = await OpenAsync();
        // Under the server default collation these would collide; utf8mb4_0900_as_cs keeps them distinct,
        // matching Postgres's case+accent-sensitive equality.
        await ExecAsync(conn, $"INSERT INTO {schema}.ITEMS VALUES ('1','Title')");
        await ExecAsync(conn, $"INSERT INTO {schema}.ITEMS VALUES ('2','title')");

        Assert.Equal(2, Convert.ToInt32(await ScalarAsync(conn, $"SELECT COUNT(*) FROM {schema}.ITEMS")));
        await Assert.ThrowsAsync<MySqlException>(() =>
            ExecAsync(conn, $"INSERT INTO {schema}.ITEMS VALUES ('3','Title')"));
    }

    [Fact]
    public async Task ForeignKeyCascadeDeletesChildren()
    {
        const string schema = "ddlb_fk";
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

        Assert.Equal(0, Convert.ToInt32(await ScalarAsync(conn, $"SELECT COUNT(*) FROM {schema}.CHILD")));
    }

    [Fact]
    public async Task SeedIsAppliedOnceAndIsIdempotent()
    {
        const string schema = "ddlb_seed";
        var settings = new AxisTable($"{schema}.SETTINGS")
            .Column("ONLY_ROW", AxisDbType.Bool, primaryKey: true, @default: AxisDefault.Bool(true), check: AxisCheck.IsTrue)
            .Column("MAX_CONCURRENT", AxisDbType.Int, notNull: true)
            .WithSeed(["ONLY_ROW", "MAX_CONCURRENT"], ["ONLY_ROW"], new object?[] { true, 20 });

        await MigrateAsync(schema, settings);
        // A second run records nothing new (version already applied) and the seed stays single-row.
        await MigrateAsync(schema, settings);

        await using var conn = await OpenAsync();
        Assert.Equal(1, Convert.ToInt32(await ScalarAsync(conn, $"SELECT COUNT(*) FROM {schema}.SETTINGS")));
        Assert.Equal(20, Convert.ToInt32(await ScalarAsync(conn, $"SELECT MAX_CONCURRENT FROM {schema}.SETTINGS")));
    }

    [Fact]
    public async Task BootstrapsOnABareServerWhenTheConnectionDatabaseDoesNotExist()
    {
        // A connection string whose default database does NOT exist yet: MySQL would refuse the connection
        // (ERROR 1049), so the runner must connect at the server level, create that database, and migrate.
        const string connectDb = "bareserver_connectdb";
        const string schema = "bareserver_schema";
        var bareServer = new MySqlConnectionStringBuilder(fixture.ConnectionString) { Database = connectDb }.ConnectionString;

        var items = new AxisTable($"{schema}.ITEMS")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true);

        // Must not throw even though `connectDb` does not exist on the server.
        await new MySqlMigrationRunner().RunAsync(bareServer, schema, [("V1", items.Render(_dialect))]);

        await using var conn = await OpenAsync();
        // The runner created BOTH the connection's named database and the migration schema, and applied V1.
        Assert.Equal(connectDb, await ScalarAsync(conn, $"SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='{connectDb}'"));
        Assert.Equal(schema, await ScalarAsync(conn, $"SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='{schema}'"));
        Assert.Equal("V1", await ScalarAsync(conn, $"SELECT VERSION FROM {schema}.MIGRATIONS"));
        Assert.NotNull(await ScalarAsync(conn, $"SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA='{schema}' AND TABLE_NAME='ITEMS'"));

        await ExecAsync(conn, $"DROP SCHEMA IF EXISTS {schema}");
        await ExecAsync(conn, $"DROP SCHEMA IF EXISTS {connectDb}");
    }
}
