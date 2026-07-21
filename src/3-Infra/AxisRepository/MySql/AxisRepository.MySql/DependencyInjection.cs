using Axis;
using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;

namespace AxisRepository.MySql;

public static class DependencyInjection
{
    /// <summary>
    /// Registers a keyed MySQL unit of work for one Bounded Context: a singleton <see cref="MySqlDataSource"/>
    /// and the scoped per-key unit of work behind both <see cref="IAxisUnitOfWork"/> and <see cref="IMySqlUnitOfWork"/>.
    /// </summary>
    public static void AddMySqlUnitOfWork(this IServiceCollection services, string serviceKey, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(serviceKey))
            throw new InvalidOperationException("MySqlUnitOfWork requires a service key.");

        // No-op outbox by default so a unit of work with no events commits normally; AddAxisOutbox* replaces it.
        services.TryAddScoped<IAxisRepositoryOutbox, NullAxisRepositoryOutbox>();
        services.AddKeyedSingleton<MySqlDataSource>(serviceKey, (_, _) => new MySqlDataSource(connectionString));
        services.AddKeyedScoped<MySqlUnitOfWorkProvider>(serviceKey);
        services.AddKeyedScoped<IAxisUnitOfWork>(serviceKey, (sp, key) => sp.GetRequiredKeyedService<MySqlUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
        services.AddKeyedScoped<IMySqlUnitOfWork>(serviceKey, (sp, key) => sp.GetRequiredKeyedService<MySqlUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
    }

    /// <summary>
    /// Registers the concrete <see cref="MySqlDbRepository"/> as the scoped <see cref="IAxisDbRepository"/>,
    /// bound to the unit of work keyed by <paramref name="serviceKey"/> (the same key passed to
    /// <see cref="AddMySqlUnitOfWork"/>). Dialect twin of <c>AddPostgresDbRepository</c>.
    /// </summary>
    public static void AddMySqlDbRepository(this IServiceCollection services, string serviceKey)
    {
        services.AddScoped<IAxisDbRepository>(sp => new MySqlDbRepository(
            sp.GetRequiredService<IAxisMediator>(),
            sp.GetRequiredService<IAxisLogger<MySqlRepositoryBase>>(),
            sp.GetRequiredKeyedService<IMySqlUnitOfWork>(serviceKey)));
    }
}
