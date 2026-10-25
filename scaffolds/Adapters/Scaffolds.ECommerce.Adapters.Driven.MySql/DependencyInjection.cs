using Scaffolds.ECommerce.Adapters.Driven.MySql.Authorization.Members;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;

namespace Scaffolds.ECommerce.Adapters.Driven.MySql;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ECommerceMySql";

    public static IServiceCollection AddECommerceMySql(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringName}.");

        services.AddMySqlUnitOfWork(ApplicationConfig.AppKey, connectionString);
        services.AddMySqlDbRepository(ApplicationConfig.AppKey);
        services.AddSingleton<IMemberSqlDialect, MySqlMemberSqlDialect>();
        return services.AddECommerceRepositories(ConnectionStringName, new MySqlSqlDialect(), new MySqlMigrationRunner());
    }
}
