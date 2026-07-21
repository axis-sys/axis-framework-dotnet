using AxisRepository.MySql;
using MySqlConnector;

namespace AxisBus.MySql.Adapters;

/// <summary>
/// Retries a dispatch-store write a few times on MySQL's transient errors (deadlock, lock-wait timeout,
/// connection blips). The transient classification is shared with the repository via
/// <see cref="MySqlTransientErrors"/>; only the loop differs — the dispatch store writes in autocommit
/// (one statement per connection), so a transient never strands a durable write and retrying in place
/// is always safe. Mirrors <c>AxisSaga.MySql.Adapters.MySqlTransientRetry</c> — there is no shared
/// <c>MySqlTransientRetry</c> in <see cref="AxisRepository.MySql"/> (only the error classification is
/// shared); each MySQL-backed store owns its own copy of the loop.
/// </summary>
internal static class MySqlTransientRetry
{
    private const int MaxAttempts = 5;

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (MySqlException ex) when (MySqlTransientErrors.IsTransient(ex) && attempt < MaxAttempts)
            {
                // Brief backoff with jitter so the racing runners desynchronize before retrying.
                await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt + Random.Shared.Next(0, 25)));
            }
        }
    }
}
