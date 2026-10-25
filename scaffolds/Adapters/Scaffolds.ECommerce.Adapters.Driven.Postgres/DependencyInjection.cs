using Scaffolds.ECommerce.Adapters.Driven.Postgres.Authorization.Members;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;

namespace Scaffolds.ECommerce.Adapters.Driven.Postgres;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ECommercePostgres";

    public static IServiceCollection AddECommercePostgres(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringName}.");

        services.AddPostgresUnitOfWork(ApplicationConfig.AppKey, connectionString);
        services.AddPostgresDbRepository(ApplicationConfig.AppKey);
        services.AddSingleton<IMemberSqlDialect, PostgresMemberSqlDialect>();
        return services.AddECommerceRepositories(ConnectionStringName, new PostgresSqlDialect(), new PostgresMigrationRunner());
    }
}
