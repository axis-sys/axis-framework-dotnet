using Axis.Ddl;

namespace AxisRepository.MySql.IntegrationTests;

// Pure (no container) assertions on the exact SQL MySqlSqlDialect emits — pins the dialect's choices:
// TINYINT(1)/JSON/DATETIME(6), UTC_TIMESTAMP(6) default, as_cs/as_ci collation, inline indexes, the
// partial-UNIQUE generated column, FK CONSTRAINT, ON DUPLICATE KEY UPDATE seeds and UTC timestamp literals.
public class MySqlSqlDialectRenderTests
{
    private static string Render(AxisTable table) => table.Render(new MySqlSqlDialect());

    [Fact]
    public void RendersMySqlTypesDefaultsAndChecks()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("FLAG", AxisDbType.Bool, notNull: true, @default: AxisDefault.Bool(true), check: AxisCheck.IsTrue)
            .Column("DATA", AxisDbType.Json)
            .Column("WHEN", AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
            .Column("AMT", AxisDbType.Decimal(10, 2))
            .Column("CNT", AxisDbType.Int, notNull: true, @default: AxisDefault.Int(5))
            .Column("UID", AxisDbType.Varchar(40), @default: AxisDefault.Raw("(UUID())")));

        Assert.Contains("FLAG TINYINT(1) NOT NULL DEFAULT 1 CHECK (FLAG = 1)", sql);
        Assert.Contains("DATA JSON", sql);
        Assert.Contains("WHEN DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6))", sql);
        Assert.Contains("AMT DECIMAL(10,2)", sql);
        Assert.Contains("CNT INT NOT NULL DEFAULT 5", sql);
        Assert.Contains("UID VARCHAR(40) DEFAULT (UUID())", sql);
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
    public void RenderAddColumnEmitsMySqlTypeTokens()
    {
        var sql = new MySqlSqlDialect()
            .RenderAddColumn("S.T", new AxisColumn("UPLOADED_AT", AxisDbType.TimestampUtc));

        Assert.Equal("ALTER TABLE S.T ADD COLUMN UPLOADED_AT DATETIME(6);", sql);
    }

    [Fact]
    public void RendersCollationPerIntent()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("CS", AxisDbType.Varchar(60), collation: AxisCollation.CaseAccentSensitive)
            .Column("CI", AxisDbType.Varchar(60), collation: AxisCollation.CaseInsensitiveAccentSensitive)
            .Column("DEF", AxisDbType.Varchar(60)));

        Assert.Contains("CS VARCHAR(60) COLLATE utf8mb4_0900_as_cs", sql);
        Assert.Contains("CI VARCHAR(60) COLLATE utf8mb4_0900_as_ci", sql);
        Assert.Contains("DEF VARCHAR(60)", sql);
        Assert.DoesNotContain("DEF VARCHAR(60) COLLATE", sql);
    }

    [Fact]
    public void RendersInlineIndexesAndDropsPartialNonUniquePredicate()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("A", AxisDbType.Varchar(50))
            .Column("B", AxisDbType.Varchar(50))
            .Index("IX_A", "A")
            .Unique("UX_B", "B")
            .PartialIndex("IX_PA", "A IS NOT NULL", "A"));

        Assert.Contains("INDEX IX_A (A)", sql);
        Assert.Contains("UNIQUE KEY UX_B (B)", sql);
        // MySQL has no filtered index: the partial NON-unique becomes a plain index, predicate dropped.
        Assert.Contains("INDEX IX_PA (A)", sql);
        Assert.DoesNotContain("IS NOT NULL", sql);
    }

    [Fact]
    public void EmulatesPartialUniqueWithGeneratedColumn()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("TENANT", AxisDbType.Varchar(50), notNull: true)
            .Column("TITLE", AxisDbType.Varchar(120), notNull: true, collation: AxisCollation.CaseAccentSensitive)
            .PartialUnique("UX_T", "IS_ACTIVE = TRUE", "TENANT", "TITLE"));

        Assert.Contains(
            "UX_T_KEY VARCHAR(120) COLLATE utf8mb4_0900_as_cs GENERATED ALWAYS AS (CASE WHEN IS_ACTIVE = TRUE THEN TITLE END) STORED",
            sql);
        Assert.Contains("UNIQUE KEY UX_T (TENANT, UX_T_KEY)", sql);
    }

    [Fact]
    public void SingleColumnPartialUniqueKeyIsJustTheGeneratedColumn()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("EMAIL", AxisDbType.Varchar(120), notNull: true)
            .PartialUnique("UX_EMAIL", "IS_VERIFIED = TRUE", "EMAIL"));

        Assert.Contains(
            "UX_EMAIL_KEY VARCHAR(120) GENERATED ALWAYS AS (CASE WHEN IS_VERIFIED = TRUE THEN EMAIL END) STORED",
            sql);
        // One indexed column: the unique key is the generated column alone.
        Assert.Contains("UNIQUE KEY UX_EMAIL (UX_EMAIL_KEY)", sql);
    }

    [Fact]
    public void RendersForeignKeyConstraintAndOnDuplicateSeed()
    {
        var fkSql = Render(new AxisTable("S.C")
            .Column("PID", AxisDbType.Varchar(50))
            .ForeignKey("FK_C_P", "PID", "S.P", "ID", onDeleteCascade: true));
        Assert.Contains("CONSTRAINT FK_C_P FOREIGN KEY (PID) REFERENCES S.P (ID) ON DELETE CASCADE", fkSql);

        var seedSql = Render(new AxisTable("S.SET")
            .Column("ONLY_ROW", AxisDbType.Bool, primaryKey: true)
            .Column("MAX", AxisDbType.Int)
            .WithSeed(["ONLY_ROW", "MAX"], ["ONLY_ROW"], new object?[] { true, 20 }));
        Assert.Contains("(1, 20)", seedSql);
        Assert.Contains("ON DUPLICATE KEY UPDATE ONLY_ROW = ONLY_ROW", seedSql);
    }

    [Fact]
    public void RendersUtcDateTimeLiteralWithoutOffset()
    {
        var sql = Render(new AxisTable("S.TS")
            .Column("ID", AxisDbType.Varchar(10), primaryKey: true)
            .Column("WHEN", AxisDbType.TimestampUtc)
            .WithSeed(["ID", "WHEN"], ["ID"],
                new object?[] { "x", new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc) }));

        Assert.Contains("'2026-06-27 12:00:00.000000'", sql);
    }
}
