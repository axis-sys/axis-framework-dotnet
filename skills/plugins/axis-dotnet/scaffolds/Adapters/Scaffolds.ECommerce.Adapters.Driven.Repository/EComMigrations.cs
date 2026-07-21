namespace Scaffolds.ECommerce.Adapters.Driven.Repository;

public static class EComMigrations
{
    public static async Task<IReadOnlyList<string>> InitializeAsync(
        string connectionString, IAxisSqlDialect sqlDialect, IAxisMigrationRunner migrationRunner)
    {
        var catalog = EComDbInit.Migrations(sqlDialect);
        await migrationRunner.RunAsync(connectionString, EComDbInit.Schema, catalog);
        return ["Catalog: " + string.Join(", ", catalog.Select(m => m.Version))];
    }
}
