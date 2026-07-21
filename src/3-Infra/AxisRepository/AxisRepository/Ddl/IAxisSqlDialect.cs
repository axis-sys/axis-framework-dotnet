namespace Axis.Ddl;

/// <summary>
/// Renders a dialect-agnostic <see cref="AxisTable"/> into one database's DDL. The dialect owns BOTH the type
/// mapping (<c>Bool</c> → <c>BOOLEAN</c>/<c>TINYINT(1)</c>, <c>Json</c> → <c>JSONB</c>/<c>JSON</c>,
/// <c>TimestampUtc</c> → <c>TIMESTAMPTZ</c>/<c>DATETIME(6)</c>) AND the layout (Postgres emits standalone
/// <c>CREATE INDEX</c> statements after the table; MySQL inlines indexes in the <c>CREATE TABLE</c> — both
/// render foreign keys as table-level named <c>CONSTRAINT</c>s). A single table definition therefore yields correct Postgres
/// or MySQL schema by swapping the injected dialect — the migration runner picks the dialect for its database.
/// Implementations live next to each adapter's migration runner (AxisRepository.Postgres / AxisRepository.MySql).
/// </summary>
public interface IAxisSqlDialect
{
    /// <summary>The full DDL for the table: the <c>CREATE TABLE</c>, its indexes, and any idempotent seed.</summary>
    string RenderCreateTable(AxisTable table);

    /// <summary>
    /// One <c>ALTER TABLE {table} ADD COLUMN …;</c> statement for an incremental migration, rendering the column
    /// line through the same pipeline as <see cref="RenderCreateTable"/> — the dialect owns the type mapping, so
    /// callers never hand-write engine tokens (<c>TIMESTAMPTZ</c> vs <c>DATETIME(6)</c>). Deliberately NOT
    /// <c>IF NOT EXISTS</c> (MySQL has no such form for <c>ADD COLUMN</c>); idempotency comes from the migration
    /// ledger, which never re-applies a recorded version. A <c>PRIMARY KEY</c> column is rejected — adding one via
    /// <c>ALTER</c> is not portable. A <c>NOT NULL</c> column without a <c>DEFAULT</c> fails on Postgres when the
    /// table has rows (MySQL backfills an implicit default) — give it a default or add it nullable.
    /// </summary>
    string RenderAddColumn(string table, AxisColumn column);
}
