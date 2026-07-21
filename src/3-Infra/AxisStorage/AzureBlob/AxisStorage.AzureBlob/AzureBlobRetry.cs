using Azure;
using Azure.Identity;

namespace AxisStorage.AzureBlob;

internal static class AzureBlobRetry
{
    private const int MaxAttempts = 5;

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                await Task.Delay(Backoff(attempt), ct);
            }
        }
    }

    public static Task ExecuteAsync(Func<Task> operation, CancellationToken ct)
        => ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, ct);

    private static bool IsTransient(Exception ex) => ex switch
    {
        AuthenticationFailedException => true,
        RequestFailedException { Status: 0 or 408 or 429 or 500 or 502 or 503 or 504 } => true,
        _ => false
    };

    private static TimeSpan Backoff(int attempt)
        => TimeSpan.FromMilliseconds((Math.Pow(2, attempt) * 100) + Random.Shared.Next(0, 100));
}
