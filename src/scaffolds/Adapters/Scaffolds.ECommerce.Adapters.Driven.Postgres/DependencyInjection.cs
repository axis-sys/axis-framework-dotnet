namespace Scaffolds.ECommerce.Adapters.Driven.Postgres;

public static class DependencyInjection
{
    // The Postgres provider: register the keyed Postgres unit of work + IAxisDbRepository, then the shared
    // repository layer. Only these two provider lines differ from AddECommerceMySql — the repository is the same.
    public static IServiceCollection AddECommercePostgres(this IServiceCollection services, string connectionString)
    {
        services.AddPostgresUnitOfWork(ApplicationConfig.AppKey, connectionString);
        services.AddPostgresDbRepository(ApplicationConfig.AppKey);
        services.AddECommerceRepositories();
        return services;
    }
}
