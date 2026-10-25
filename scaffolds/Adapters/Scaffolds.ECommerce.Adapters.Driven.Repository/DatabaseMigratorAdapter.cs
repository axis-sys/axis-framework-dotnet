using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers;
using Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation;
using Scaffolds.ECommerce.Contracts.Driven.Admin;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository;

internal sealed class DatabaseMigratorAdapter(
    IConfiguration configuration,
    IAxisLogger<DatabaseMigratorAdapter> logger,
    string connectionStringName,
    IAxisSqlDialect sqlDialect,
    IAxisMigrationRunner migrationRunner
) : IDatabaseMigratorPort
{
    private (string Schema, (string Version, string Script)[] Migrations)[] BoundedContexts =>
    [
        (CustomersDbInit.Schema, CustomersDbInit.Migrations(sqlDialect)),
        (CatalogDbInit.Schema, CatalogDbInit.Migrations(sqlDialect)),
        (EmailValidationDbInit.Schema, EmailValidationDbInit.Migrations(sqlDialect)),
        (AuthorizationDbInit.Schema, AuthorizationDbInit.Migrations(sqlDialect)),
    ];

    public Task<AxisResult<IReadOnlyList<string>>> MigrateAsync()
        => configuration.GetConnectionString(connectionStringName).Rop()
            .Ensure(connectionString => !string.IsNullOrWhiteSpace(connectionString), AdminErrors.DatabaseConnectionStringMissing)
            .ThenAsync(connectionString => AxisResult.TryAsync(() => RunAsync(connectionString!), Failed));

    private async Task<IReadOnlyList<string>> RunAsync(string connectionString)
    {
        var boundedContexts = BoundedContexts;
        foreach (var (schema, migrations) in boundedContexts)
            await migrationRunner.RunAsync(connectionString, schema, migrations);

        return [.. boundedContexts.Select(boundedContext => boundedContext.Schema)];
    }

    private AxisError Failed(Exception exception)
    {
        logger.LogError(exception, "Database migration failed", ("connectionStringName", connectionStringName));
        return AdminErrors.DatabaseMigrationFailed;
    }
}
