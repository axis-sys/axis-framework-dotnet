using Axis.Ddl;

namespace AxisRepository.UnitTests;

// The dialect-agnostic DDL model: the type/default/check factories and the AxisTable fluent builder.
// Pure, no database — every adapter renders this same model, so it must be exactly what the builder records.
public class AxisDdlModelTests
{
    [Fact]
    public void VarcharCarriesItsLength()
    {
        var type = Assert.IsType<AxisDbType.VarcharType>(AxisDbType.Varchar(120));
        Assert.Equal(120, type.Length);
    }

    [Fact]
    public void DecimalCarriesPrecisionAndScale()
    {
        var type = Assert.IsType<AxisDbType.DecimalType>(AxisDbType.Decimal(18, 4));
        Assert.Equal(18, type.Precision);
        Assert.Equal(4, type.Scale);
    }

    [Fact]
    public void ScalarTypesAreSingletons()
    {
        Assert.Same(AxisDbType.Text, AxisDbType.Text);
        Assert.Same(AxisDbType.Int, AxisDbType.Int);
        Assert.Same(AxisDbType.Bool, AxisDbType.Bool);
        Assert.Same(AxisDbType.Json, AxisDbType.Json);
        Assert.Same(AxisDbType.TimestampUtc, AxisDbType.TimestampUtc);
    }

    [Fact]
    public void DefaultFactoriesProduceTheRightVariants()
    {
        Assert.IsType<AxisDefault.NowUtcDefault>(AxisDefault.NowUtc);
        Assert.True(((AxisDefault.BoolDefault)AxisDefault.Bool(true)).Value);
        Assert.Equal(20, ((AxisDefault.IntDefault)AxisDefault.Int(20)).Value);
        Assert.Equal("gen_random_uuid()", ((AxisDefault.RawDefault)AxisDefault.Raw("gen_random_uuid()")).Sql);
    }

    [Fact]
    public void IsTrueCheckIsASingleton()
    {
        Assert.IsType<AxisCheck.IsTrueCheck>(AxisCheck.IsTrue);
        Assert.Same(AxisCheck.IsTrue, AxisCheck.IsTrue);
    }

    [Fact]
    public void ColumnRecordsEveryAttribute()
    {
        var table = new AxisTable("S.T")
            .Column("ID", AxisDbType.Varchar(50), primaryKey: true)
            .Column("TITLE", AxisDbType.Varchar(120), notNull: true,
                collation: AxisCollation.CaseAccentSensitive)
            .Column("IS_ACTIVE", AxisDbType.Bool, notNull: true,
                @default: AxisDefault.Bool(true), check: AxisCheck.IsTrue);

        Assert.Equal("S.T", table.Name);
        Assert.Equal(3, table.Columns.Count);

        var id = table.Columns[0];
        Assert.Equal("ID", id.Name);
        Assert.True(id.PrimaryKey);
        Assert.False(id.NotNull);

        var title = table.Columns[1];
        Assert.True(title.NotNull);
        Assert.Equal(AxisCollation.CaseAccentSensitive, title.Collation);

        var active = table.Columns[2];
        Assert.Same(AxisCheck.IsTrue, active.Check);
        Assert.IsType<AxisDefault.BoolDefault>(active.Default);
    }

    [Fact]
    public void IndexBuildersRecordUniquenessAndPredicate()
    {
        var table = new AxisTable("S.T")
            .Index("IX_A", "A")
            .Unique("UX_B", "B", "C")
            .PartialIndex("IX_P", "DELETED_AT IS NULL", "D")
            .PartialUnique("UX_P", "IS_ACTIVE = TRUE", "E", "F");

        var ix = table.Indexes[0];
        Assert.False(ix.Unique);
        Assert.Null(ix.PartialPredicate);
        Assert.Equal(["A"], ix.Columns);

        var ux = table.Indexes[1];
        Assert.True(ux.Unique);
        Assert.Equal(["B", "C"], ux.Columns);

        var partial = table.Indexes[2];
        Assert.False(partial.Unique);
        Assert.Equal("DELETED_AT IS NULL", partial.PartialPredicate);

        var partialUnique = table.Indexes[3];
        Assert.True(partialUnique.Unique);
        Assert.Equal("IS_ACTIVE = TRUE", partialUnique.PartialPredicate);
        Assert.Equal(["E", "F"], partialUnique.Columns);
    }

    [Fact]
    public void ForeignKeyBuilderRecordsCascade()
    {
        var table = new AxisTable("S.CHILD")
            .ForeignKey("FK_CHILD_PARENT", "PARENT_ID", "S.PARENT", "ID", onDeleteCascade: true);

        var fk = Assert.Single(table.ForeignKeys);
        Assert.Equal("FK_CHILD_PARENT", fk.Name);
        Assert.Equal("PARENT_ID", fk.Column);
        Assert.Equal("S.PARENT", fk.ReferencedTable);
        Assert.Equal("ID", fk.ReferencedColumn);
        Assert.True(fk.OnDeleteCascade);
    }

    [Fact]
    public void CheckBuilderRecordsTableCheck()
    {
        var table = new AxisTable("S.T").Check("CK_POSITIVE", "QTY > 0");

        var check = Assert.Single(table.Checks);
        Assert.Equal("CK_POSITIVE", check.Name);
        Assert.Equal("QTY > 0", check.Expression);
    }

    [Fact]
    public void WithSeedRecordsColumnsConflictAndRows()
    {
        var table = new AxisTable("S.SETTINGS")
            .Column("ONLY_ROW", AxisDbType.Bool, primaryKey: true)
            .Column("MAX", AxisDbType.Int)
            .WithSeed(["ONLY_ROW", "MAX"], ["ONLY_ROW"], new object?[] { true, 20 });

        var seed = table.Seed!;
        Assert.Equal(["ONLY_ROW", "MAX"], seed.Columns);
        Assert.Equal(["ONLY_ROW"], seed.ConflictColumns);
        var row = Assert.Single(seed.Rows);
        Assert.Equal(new object?[] { true, 20 }, row);
    }

    [Fact]
    public void FluentBuilderReturnsTheSameInstance()
    {
        var table = new AxisTable("S.T");
        Assert.Same(table, table.Column("A", AxisDbType.Int));
        Assert.Same(table, table.Index("IX", "A"));
        Assert.Same(table, table.Unique("UX", "A"));
        Assert.Same(table, table.PartialIndex("IXP", "A IS NOT NULL", "A"));
        Assert.Same(table, table.PartialUnique("UXP", "A IS NOT NULL", "A"));
        Assert.Same(table, table.ForeignKey("FK", "A", "S.O", "B"));
        Assert.Same(table, table.WithSeed(["A"], ["A"], new object?[] { 1 }));
    }
}
