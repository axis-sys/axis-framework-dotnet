using AxisRepository.MySql;
using MySqlConnector;

namespace AxisSaga.MySql.Adapters;

/// <summary>
/// Retries a saga-store write a few times on MySQL's transient errors (deadlock, lock-wait timeout,
/// connection blips). The transient classification is shared with the repository via
/// <see cref="MySqlTransientErrors"/>; only the loop differs — the saga store writes in autocommit
/// (one statement per connection), so a transient never strands a durable write and retrying in place
/// is always safe (unlike the repository base, which must surface transients once its unit-of-work holds
/// an uncommitted write).
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
