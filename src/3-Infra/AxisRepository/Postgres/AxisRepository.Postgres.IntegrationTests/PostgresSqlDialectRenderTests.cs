using Axis.Ddl;

namespace AxisRepository.Postgres.IntegrationTests;

// Pure (no container) assertions on the exact SQL PostgresSqlDialect emits — pins BOOLEAN/JSONB/TIMESTAMPTZ,
// NOW() and boolean defaults, CHECK without "= 1", no COLLATE (Postgres is sensitive by default), standalone
// CREATE [UNIQUE] INDEX with WHERE for partials, FK CONSTRAINT, ON CONFLICT DO NOTHING and offset-pinned
// timestamp literals.
public class PostgresSqlDialectRenderTests
{
    private static string Render(AxisTable table) => table.Render(new PostgresSqlDialect());

    [Fact]
    public void RendersPostgresTypesDefaultsAndChecks()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("FLAG", AxisDbType.Bool, notNull: true, @default: AxisDefault.Bool(true), check: AxisCheck.IsTrue)
            .Column("DATA", AxisDbType.Json)
            .Column("WHEN", AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
            .Column("AMT", AxisDbType.Decimal(10, 2))
            .Column("CNT", AxisDbType.Int, notNull: true, @default: AxisDefault.Int(5))
            .Column("UID", AxisDbType.Varchar(40), @default: AxisDefault.Raw("gen_random_uuid()")));

        Assert.Contains("FLAG BOOLEAN NOT NULL DEFAULT TRUE CHECK (FLAG)", sql);
        Assert.Contains("DATA JSONB", sql);
        Assert.Contains("WHEN TIMESTAMPTZ NOT NULL DEFAULT NOW()", sql);
        Assert.Contains("AMT NUMERIC(10,2)", sql);
        Assert.Contains("CNT INT NOT NULL DEFAULT 5", sql);
        Assert.Contains("UID VARCHAR(40) DEFAULT gen_random_uuid()", sql);
    }

    [Fact]
    public void RendersCompositePrimaryKeyAsTableLevelConstraint()
    {
        var sql = Render(new AxisTable("S.PROVIDER_LINKS")
            .Column("PROVIDER", AxisDbType.Varchar(50), notNull: true)
            .Column("EXTERNAL_ID", AxisDbType.Varchar(100), notNull: true)
            .PrimaryKey("PROVIDER", "EXTERNAL_ID"));

        Assert.Contains("PRIMARY KEY (PROVIDER, EXTERNAL_ID)", sql);
    }

    [Fact]
    public void RenderAddColumnEmitsPostgresTypeTokens()
    {
        var sql = new PostgresSqlDialect()
            .RenderAddColumn("S.T", new AxisColumn("UPLOADED_AT", AxisDbType.TimestampUtc));

        Assert.Equal("ALTER TABLE S.T ADD COLUMN UPLOADED_AT TIMESTAMPTZ;", sql);
    }

    [Fact]
    public void EmitsNoCollation()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("CS", AxisDbType.Varchar(60), collation: AxisCollation.CaseAccentSensitive)
            .Column("CI", AxisDbType.Varchar(60), collation: AxisCollation.CaseInsensitiveAccentSensitive));

        Assert.Contains("CS VARCHAR(60)", sql);
        Assert.Contains("CI VARCHAR(60)", sql);
        Assert.DoesNotContain("COLLATE", sql);
    }

    [Fact]
    public void RendersStandaloneIndexesWithPartialPredicate()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("TENANT", AxisDbType.Varchar(50))
            .Column("TITLE", AxisDbType.Varchar(120))
            .Column("FLAG", AxisDbType.Bool)
            .Index("IX_A", "TITLE")
            .Unique("UX_B", "TENANT")
            .PartialIndex("IX_PA", "FLAG = TRUE", "TITLE")
            .PartialUnique("UX_P", "FLAG = TRUE", "TENANT", "TITLE"));

        Assert.Contains("INDEX IF NOT EXISTS IX_A ON S.T (TITLE)", sql);
        Assert.Contains("INDEX IF NOT EXISTS UX_B ON S.T (TENANT)", sql);
        Assert.Contains("INDEX IF NOT EXISTS IX_PA ON S.T (TITLE)", sql);
        Assert.Contains("INDEX IF NOT EXISTS UX_P ON S.T (TENANT, TITLE)", sql);
        // Partials keep their predicate as a real filtered index; uniques carry the UNIQUE keyword.
        Assert.Contains("WHERE FLAG = TRUE", sql);
        Assert.Contains("UNIQUE", sql);
    }

    [Fact]
    public void RendersForeignKeyConstraintAndOnConflictSeed()
    {
        var fkSql = Render(new AxisTable("S.C")
            .Column("PID", AxisDbType.Varchar(50))
            .ForeignKey("FK_C_P", "PID", "S.P", "ID", onDeleteCascade: true));
        Assert.Contains("CONSTRAINT FK_C_P FOREIGN KEY (PID) REFERENCES S.P (ID) ON DELETE CASCADE", fkSql);

        var seedSql = Render(new AxisTable("S.SET")
            .Column("ONLY_ROW", AxisDbType.Bool, primaryKey: true)
            .Column("MAX", AxisDbType.Int)
            .WithSeed(["ONLY_ROW", "MAX"], ["ONLY_ROW"], new object?[] { true, 20 }));
        Assert.Contains("(TRUE, 20)", seedSql);
        Assert.Contains("ON CONFLICT (ONLY_ROW) DO NOTHING", seedSql);
    }

    [Fact]
    public void RendersUtcDateTimeLiteralWithOffset()
    {
        var sql = Render(new AxisTable("S.TS")
            .Column("ID", AxisDbType.Varchar(10), primaryKey: true)
            .Column("WHEN", AxisDbType.TimestampUtc)
            .WithSeed(["ID", "WHEN"], ["ID"],
                new object?[] { "x", new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc) }));

        Assert.Contains("TIMESTAMPTZ '2026-06-27 12:00:00.000000+00'", sql);
    }
}
