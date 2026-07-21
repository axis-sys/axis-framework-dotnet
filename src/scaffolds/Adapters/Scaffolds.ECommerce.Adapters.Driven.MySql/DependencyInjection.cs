namespace Scaffolds.ECommerce.Adapters.Driven.MySql;

public static class DependencyInjection
{
    // The MySQL provider: mirror of AddECommercePostgres — swap the two provider lines (MySql unit of work +
    // MySql IAxisDbRepository), keep the exact same shared repository layer. That symmetry is the whole point:
    // the domain repository is written once and runs on either database.
    public static IServiceCollection AddECommerceMySql(this IServiceCollection services, string connectionString)
    {
        services.AddMySqlUnitOfWork(ApplicationConfig.AppKey, connectionString);
        services.AddMySqlDbRepository(ApplicationConfig.AppKey);
        services.AddECommerceRepositories();
        return services;
    }
}
