using System.Collections.Concurrent;
using Axis;
using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisRepository.Postgres;

/// <summary>
/// Caches one <see cref="PostgresUnitOfWork"/> per service key within the DI scope, so the keyed
/// <see cref="IAxisUnitOfWork"/> and <see cref="IPostgresUnitOfWork"/> registrations resolve to the same instance.
/// </summary>
public sealed class PostgresUnitOfWorkProvider
{
    private readonly ConcurrentDictionary<object, PostgresUnitOfWork> _unitOfWorks = new();

    // GetOrAdd (not TryGetValue + TryAdd) so concurrent callers in the same scope all observe the single
    // winning instance — never a discarded local copy with its own connection/transaction.
    public PostgresUnitOfWork GetUnitOfWork(IServiceProvider sp, object? key)
        => _unitOfWorks.GetOrAdd(key!, static (resolvedKey, provider) => new PostgresUnitOfWork(
            provider.GetRequiredService<IAxisMediator>(),
            provider.GetRequiredKeyedService<NpgsqlDataSource>(resolvedKey),
            provider.GetRequiredService<IAxisTelemetry>(),
            provider.GetRequiredService<IAxisLogger<PostgresUnitOfWork>>(),
            provider.GetRequiredService<IAxisRepositoryOutbox>()), sp);
}
