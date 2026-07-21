using Axis.Ddl;

namespace AxisCache.Repository.Persistence;

/// <summary>
/// The AxisCache L2 store schema, declared ONCE and dialect-agnostic — mirrors the AxisSaga schema pattern.
/// Each adapter renders it with its own <see cref="IAxisSqlDialect"/> and applies it with the framework
/// <see cref="IAxisMigrationRunner"/>. In MySQL a SCHEMA is a database; the runner issues
/// <c>CREATE SCHEMA IF NOT EXISTS</c> for it.
/// </summary>
public static class AxisCacheSchema
{
    public const string Schema = CacheEntriesTable.Schema;

    /// <summary>The migration set for the given dialect — a single consolidated V1 that renders the table.</summary>
    public static (string Version, string Script)[] Migrations(IAxisSqlDialect dialect) =>
    [
        ("V1", CacheEntriesTable.Define().Render(dialect)),
    ];
}
