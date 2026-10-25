using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers;
using Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation;
using Scaffolds.ECommerce.Contracts.Driven.Admin;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddECommerceRepositories(
        this IServiceCollection services,
        string connectionStringName,
        IAxisSqlDialect sqlDialect,
        IAxisMigrationRunner migrationRunner)
    {
        services.AddScoped<IUnitOfWork, UnitOfWorkAdapter>();
        services.AddScoped<IDatabaseMigratorPort>(provider =>
            ActivatorUtilities.CreateInstance<DatabaseMigratorAdapter>(provider, connectionStringName, sqlDialect, migrationRunner));

        return services
            .AddCatalogRepositories()
            .AddCustomersRepositories()
            .AddEmailValidationRepositories()
            .AddAuthorizationRepositories();
    }
}
