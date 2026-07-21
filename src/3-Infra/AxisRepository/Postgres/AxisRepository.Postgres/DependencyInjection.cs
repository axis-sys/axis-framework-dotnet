using Axis;
using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AxisRepository.Postgres;

public static class DependencyInjection
{
    /// <summary>
    /// Registers a keyed Postgres unit of work for one Bounded Context:
    /// a singleton <see cref="Npgsql.NpgsqlDataSource"/> and the scoped per-key unit of work behind both
    /// <see cref="IAxisUnitOfWork"/> and <see cref="IPostgresUnitOfWork"/>.
    /// </summary>
    public static void AddPostgresUnitOfWork(this IServiceCollection services, string serviceKey, string connectionString)
    {
        services.AddNpgsqlDataSource(connectionString,
            connectionLifetime: ServiceLifetime.Scoped,
            dataSourceLifetime: ServiceLifetime.Singleton,
            serviceKey: serviceKey);

        // No-op outbox by default so a unit of work with no events commits normally; AddAxisOutbox* replaces it.
        services.TryAddScoped<IAxisRepositoryOutbox, NullAxisRepositoryOutbox>();
        services.AddKeyedScoped<PostgresUnitOfWorkProvider>(serviceKey);
        services.AddKeyedScoped<IAxisUnitOfWork>(serviceKey, (sp, key) => sp.GetRequiredKeyedService<PostgresUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
        services.AddKeyedScoped<IPostgresUnitOfWork>(serviceKey, (sp, key) => sp.GetRequiredKeyedService<PostgresUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
    }

    /// <summary>
    /// Registers the concrete <see cref="PostgresDbRepository"/> as the scoped <see cref="IAxisDbRepository"/>,
    /// bound to the unit of work keyed by <paramref name="serviceKey"/> (the same key passed to
    /// <see cref="AddPostgresUnitOfWork"/>). Lets a shared, dialect-agnostic repository layer compose one
    /// Postgres executor without each application writing its own keyed subclass.
    /// </summary>
    public static void AddPostgresDbRepository(this IServiceCollection services, string serviceKey)
    {
        services.AddScoped<IAxisDbRepository>(sp => new PostgresDbRepository(
            sp.GetRequiredService<IAxisMediator>(),
            sp.GetRequiredService<IAxisLogger<PostgresRepositoryBase>>(),
            sp.GetRequiredKeyedService<IPostgresUnitOfWork>(serviceKey)));
    }
}
