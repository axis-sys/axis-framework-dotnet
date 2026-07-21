using Axis.Ddl;

namespace AxisRepository.UnitTests;

// Exercises the shared rendering skeleton in AxisSqlDialectBase through a minimal concrete dialect, so the
// CREATE TABLE assembly, column-line ordering, value rendering (incl. UTC-safe timestamps and the null hook)
// and the reusable ForeignKeyConstraint helper are covered without any database.
public class AxisSqlDialectBaseTests
{
    private class TestDialect : AxisSqlDialectBase
    {
        protected override string RenderType(AxisDbType dbType) => dbType switch
        {
            AxisDbType.VarcharType v => $"VARCHAR({v.Length})",
            AxisDbType.TextType => "TEXT",
            AxisDbType.IntType => "INT",
            AxisDbType.BoolType => "BOOL",
            AxisDbType.JsonType => "JSON",
            AxisDbType.TimestampUtcType => "TS",
            AxisDbType.DecimalType d => $"DEC({d.Precision},{d.Scale})",
            _ => throw new NotSupportedException(),
        };

        protected override string RenderDefault(AxisDefault @default) => @default switch
        {
            AxisDefault.NowUtcDefault => "NOW",
            AxisDefault.BoolDefault b => b.Value ? "T" : "F",
            AxisDefault.IntDefault i => i.Value.ToString(),
            AxisDefault.RawDefault r => r.Sql,
            _ => throw new NotSupportedException(),
        };

        protected override string RenderCheck(AxisCheck check, string column) => column;
        protected override string RenderCollation(AxisCollation collation)
            => collation == AxisCollation.Default ? "" : $" COLL_{collation}";
        protected override string RenderBoolLiteral(bool value) => value ? "TRUE" : "FALSE";
        protected override string RenderSeedConflict(AxisSeed seed) => $"ON CONFLICT ({string.Join(", ", seed.ConflictColumns)})";
        protected override IEnumerable<string> RenderInlineIndexLines(AxisTable table) => [];
        protected override IEnumerable<string> RenderPostTableStatements(AxisTable table) => [];
        protected override string RenderForeignKey(AxisForeignKey fk) => ForeignKeyConstraint(fk);
        protected override string RenderTimestampLiteral(DateTimeOffset utc) => $"TS'{FormatUtcTimestamp(utc)}'";
    }

    // A dialect whose null token differs, proving the RenderNull hook is honoured.
    private sealed class NilDialect : TestDialect
    {
        protected override string RenderNull() => "NIL";
    }

    private static string Render(AxisTable table) => new TestDialect().RenderCreateTable(table);

    [Fact]
    public void RendersCreateTableHeaderAndClosing()
    {
        var sql = Render(new AxisTable("S.T").Column("ID", AxisDbType.Int, primaryKey: true));

        Assert.Contains("CREATE TABLE IF NOT EXISTS S.T", sql);
        Assert.Contains("(", sql);
        Assert.Contains(");", sql);
    }

    [Fact]
    public void ColumnLineOrdersTypeCollationThenConstraintThenDefaultThenCheck()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("TITLE", AxisDbType.Varchar(120), notNull: true, collation: AxisCollation.CaseAccentSensitive)
            .Column("FLAG", AxisDbType.Bool, notNull: true, @default: AxisDefault.Bool(true), check: AxisCheck.IsTrue)
            .Column("NOTE", AxisDbType.Text));

        Assert.Contains("ID VARCHAR(50) PRIMARY KEY", sql);
        Assert.Contains("TITLE VARCHAR(120) COLL_CaseAccentSensitive NOT NULL", sql);
        Assert.Contains("FLAG BOOL NOT NULL DEFAULT T CHECK (FLAG)", sql);
        // Nullable, no default: just name + type.
        Assert.Contains("NOTE TEXT", sql);
        Assert.DoesNotContain("NOTE TEXT NOT NULL", sql);
    }

    [Fact]
    public void PrimaryKeyWinsOverNotNullOnTheSameColumn()
    {
        var sql = Render(new AxisTable("S.T").Column("ID", AxisDbType.Int, notNull: true, primaryKey: true));

        Assert.Contains("ID INT PRIMARY KEY", sql);
        Assert.DoesNotContain("PRIMARY KEY NOT NULL", sql);
    }

    [Fact]
    public void ForeignKeyConstraintRendersWithAndWithoutCascade()
    {
        var cascade = Render(new AxisTable("S.C")
            .Column("PID", AxisDbType.Int)
            .ForeignKey("FK_C_P", "PID", "S.P", "ID", onDeleteCascade: true));
        Assert.Contains("CONSTRAINT FK_C_P FOREIGN KEY (PID) REFERENCES S.P (ID) ON DELETE CASCADE", cascade);

        var noCascade = Render(new AxisTable("S.C")
            .Column("PID", AxisDbType.Int)
            .ForeignKey("FK_C_P", "PID", "S.P", "ID"));
        Assert.Contains("CONSTRAINT FK_C_P FOREIGN KEY (PID) REFERENCES S.P (ID)", noCascade);
        Assert.DoesNotContain("ON DELETE CASCADE", noCascade);
    }

    [Fact]
    public void RenderAddColumnEmitsOneAlterStatementThroughTheColumnPipeline()
    {
        var sql = new TestDialect().RenderAddColumn("S.T", new AxisColumn("UPLOADED_AT", AxisDbType.TimestampUtc));

        Assert.Equal("ALTER TABLE S.T ADD COLUMN UPLOADED_AT TS;", sql);
    }

    [Fact]
    public void RenderAddColumnKeepsColumnLineTokensOrderedAsInCreateTable()
    {
        var sql = new TestDialect().RenderAddColumn("S.T", new AxisColumn(
            "FLAG", AxisDbType.Bool, NotNull: true, Default: AxisDefault.Bool(true), Check: AxisCheck.IsTrue));

        Assert.Equal("ALTER TABLE S.T ADD COLUMN FLAG BOOL NOT NULL DEFAULT T CHECK (FLAG);", sql);
    }

    [Fact]
    public void RenderAddColumnRendersCollation()
    {
        var sql = new TestDialect().RenderAddColumn("S.T", new AxisColumn(
            "TITLE", AxisDbType.Varchar(120), Collation: AxisCollation.CaseAccentSensitive));

        Assert.Equal("ALTER TABLE S.T ADD COLUMN TITLE VARCHAR(120) COLL_CaseAccentSensitive;", sql);
    }

    [Fact]
    public void RenderAddColumnRejectsPrimaryKey()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new TestDialect().RenderAddColumn("S.T", new AxisColumn("ID", AxisDbType.Int, PrimaryKey: true)));

        Assert.Contains("PRIMARY KEY", ex.Message);
    }

    [Fact]
    public void CompositePrimaryKeyRendersAsSingleTableLevelConstraint()
    {
        var sql = Render(new AxisTable("S.PROVIDER_LINKS")
            .Column("PROVIDER", AxisDbType.Varchar(50), notNull: true)
            .Column("EXTERNAL_ID", AxisDbType.Varchar(100), notNull: true)
            .PrimaryKey("PROVIDER", "EXTERNAL_ID"));

        Assert.Contains("PRIMARY KEY (PROVIDER, EXTERNAL_ID)", sql);
        // Exactly one PRIMARY KEY clause in the whole statement — never one per column.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(sql, "PRIMARY KEY"));
    }

    [Fact]
    public void CompositePrimaryKeyRejectsFewerThanTwoColumns()
    {
        var ex = Assert.Throws<ArgumentException>(() => new AxisTable("S.T").PrimaryKey("ONLY_ONE"));

        Assert.Contains("at least 2 columns", ex.Message);
    }

    [Fact]
    public void CompositePrimaryKeyConflictingWithColumnLevelFlagThrowsAtRenderTime()
    {
        var table = new AxisTable("S.T")
            .Column("A", AxisDbType.Int, primaryKey: true)
            .Column("B", AxisDbType.Int)
            .PrimaryKey("A", "B");

        var ex = Assert.Throws<InvalidOperationException>(() => Render(table));

        Assert.Contains("S.T", ex.Message);
        Assert.Contains("PrimaryKey", ex.Message);
    }

    [Fact]
    public void TableLevelCheckRendersAsNamedConstraint()
    {
        var sql = Render(new AxisTable("S.T")
            .Column("A", AxisDbType.Varchar(10))
            .Column("B", AxisDbType.Varchar(10))
            .Check("CK_A_XOR_B", "(A IS NOT NULL AND B IS NULL) OR (A IS NULL AND B IS NOT NULL)"));

        Assert.Contains(
            "CONSTRAINT CK_A_XOR_B CHECK ((A IS NOT NULL AND B IS NULL) OR (A IS NULL AND B IS NOT NULL))",
            sql);
    }

    [Fact]
    public void SeedRendersInsertValuesAndConflictClause()
    {
        var sql = Render(new AxisTable("S.SETTINGS")
            .Column("ONLY_ROW", AxisDbType.Bool, primaryKey: true)
            .Column("MAX", AxisDbType.Int)
            .WithSeed(["ONLY_ROW", "MAX"], ["ONLY_ROW"], new object?[] { true, 20 }));

        Assert.Contains("INSERT INTO S.SETTINGS (ONLY_ROW, MAX) VALUES", sql);
        Assert.Contains("(TRUE, 20)", sql);
        Assert.Contains("ON CONFLICT (ONLY_ROW)", sql);
    }

    [Fact]
    public void RenderValueHandlesEveryScalarKind()
    {
        var sql = Render(new AxisTable("S.V")
            .Column("X", AxisDbType.Int)
            .WithSeed(["A", "B", "C", "D", "E"], ["A"],
                new object?[] { null, true, "O'Brien", 7, 9_999_999_999L }));

        Assert.Contains("(NULL, TRUE, 'O''Brien', 7, 9999999999)", sql);
    }

    [Fact]
    public void RenderValueRendersNumericTypesUnquotedAndInvariant()
    {
        // decimal/double/float are numeric literals (NOT quoted) and ALWAYS invariant — a culture-specific
        // comma (e.g. "1,5" on de-DE) would corrupt the value or break the INSERT.
        var sql = Render(new AxisTable("S.V").Column("X", AxisDbType.Decimal(10, 2))
            .WithSeed(["A", "B", "C"], ["A"], new object?[] { 12.5m, 3.25d, 1.5f }));

        Assert.Contains("(12.5, 3.25, 1.5)", sql);
    }

    // A type that does NOT implement IFormattable, to exercise RenderValue's final quoted-ToString fallback.
    private sealed record NonFormattable(string Text)
    {
        public override string ToString() => Text;
    }

    [Fact]
    public void RenderValueQuotesFormattableAndOtherTypes()
    {
        // A Guid is IFormattable -> quoted with an invariant ToString; a custom non-IFormattable value falls
        // through to the final quoted-ToString arm. Both arms are exercised.
        var id = new Guid("11111111-1111-1111-1111-111111111111");
        var sql = Render(new AxisTable("S.V").Column("X", AxisDbType.Varchar(40))
            .WithSeed(["A", "B"], ["A"], new object?[] { id, new NonFormattable("hi") }));

        Assert.Contains("('11111111-1111-1111-1111-111111111111', 'hi')", sql);
    }

    [Fact]
    public void RenderNullHookIsHonoured()
    {
        var table = new AxisTable("S.V")
            .Column("X", AxisDbType.Int)
            .WithSeed(["A"], ["A"], [new object?[] { null }]);

        Assert.Contains("(NULL)", new TestDialect().RenderCreateTable(table));
        Assert.Contains("(NIL)", new NilDialect().RenderCreateTable(table));
    }

    [Fact]
    public void DateTimeOffsetIsNormalisedToUtc()
    {
        // 09:00 at -03:00 is 12:00 UTC — the literal must carry the UTC wall-clock, not the local one.
        var instant = new DateTimeOffset(2026, 6, 27, 9, 0, 0, TimeSpan.FromHours(-3));
        var sql = Render(new AxisTable("S.V").Column("X", AxisDbType.TimestampUtc)
            .WithSeed(["A"], ["A"], [new object?[] { instant }]));

        Assert.Contains("TS'2026-06-27 12:00:00.000000'", sql);
    }

    [Fact]
    public void UtcDateTimeRendersUnchanged()
    {
        var dt = new DateTime(2026, 6, 27, 15, 30, 0, DateTimeKind.Utc);
        var sql = Render(new AxisTable("S.V").Column("X", AxisDbType.TimestampUtc)
            .WithSeed(["A"], ["A"], [new object?[] { dt }]));

        Assert.Contains("TS'2026-06-27 15:30:00.000000'", sql);
    }

    [Fact]
    public void UnspecifiedDateTimeIsTakenAsUtc()
    {
        var dt = new DateTime(2026, 6, 27, 8, 15, 0, DateTimeKind.Unspecified);
        var sql = Render(new AxisTable("S.V").Column("X", AxisDbType.TimestampUtc)
            .WithSeed(["A"], ["A"], [new object?[] { dt }]));

        Assert.Contains("TS'2026-06-27 08:15:00.000000'", sql);
    }

    [Fact]
    public void LocalDateTimeIsConvertedToUtc()
    {
        // Build a Local DateTime for a known instant, independent of the machine's zone.
        var instant = new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);
        var local = instant.LocalDateTime; // Kind = Local
        var sql = Render(new AxisTable("S.V").Column("X", AxisDbType.TimestampUtc)
            .WithSeed(["A"], ["A"], [new object?[] { local }]));

        Assert.Contains("TS'2026-06-27 12:00:00.000000'", sql);
    }
}
