using System.Collections.Concurrent;
using Axis;
using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace AxisRepository.MySql;

/// <summary>
/// Caches one <see cref="MySqlUnitOfWork"/> per service key within the DI scope, so the keyed
/// <see cref="IAxisUnitOfWork"/> and <see cref="IMySqlUnitOfWork"/> registrations resolve to the same instance.
/// </summary>
internal sealed class MySqlUnitOfWorkProvider
{
    private readonly ConcurrentDictionary<object, MySqlUnitOfWork> _unitOfWorks = new();

    // GetOrAdd (not TryGetValue + TryAdd) so concurrent callers in the same scope all observe the single
    // winning instance — never a discarded local copy with its own connection/transaction.
    public MySqlUnitOfWork GetUnitOfWork(IServiceProvider sp, object? key)
        => _unitOfWorks.GetOrAdd(key ?? throw new ArgumentException("MySql uow key is required", nameof(key)),
                static (resolvedKey, provider) => new MySqlUnitOfWork(
                    provider.GetRequiredService<IAxisMediator>(),
                    provider.GetRequiredKeyedService<MySqlDataSource>(resolvedKey),
                    provider.GetRequiredService<IAxisTelemetry>(),
                    provider.GetRequiredService<IAxisLogger<MySqlUnitOfWork>>(),
                    provider.GetRequiredService<IAxisRepositoryOutbox>())
                , sp);
}
