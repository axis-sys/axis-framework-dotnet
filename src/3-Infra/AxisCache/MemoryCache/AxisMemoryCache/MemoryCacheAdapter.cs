using Axis;
using AxisMediator.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace AxisMemoryCache;

public class MemoryCacheAdapter(IMemoryCache memoryCache, IAxisMediatorAccessor mediatorAccessor) : IAxisCache
{
    private readonly CancellationToken _cancellationToken = mediatorAccessor.AxisMediator?.CancellationToken ?? CancellationToken.None;
    public Task<AxisResult<T?>> GetAsync<T>(string key)
    {
        return AxisResult.TryAsync(() =>
        {
            _cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(memoryCache.Get<T>(key));
        });
    }

    public Task<AxisResult> SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        return AxisResult.TryAsync(() =>
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (expiration.HasValue)
                memoryCache.Set(key, value, expiration.Value);
            else
                memoryCache.Set(key, value);

            return Task.CompletedTask;
        });
    }

    public async Task<AxisResult<T>> GetOrCreateAsync<T>(string key, Func<Task<AxisResult<T>>> factory, TimeSpan? expiration = null)
    {
        try
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (memoryCache.TryGetValue(key, out T? value))
                return AxisResult.Ok(value!);

            var result = await factory();
            if (result.IsFailure)
                return result;

            if (expiration.HasValue)
                memoryCache.Set(key, result.Value, expiration.Value);
            else
                memoryCache.Set(key, result.Value);

            return result;
        }
        catch (Exception ex)
        {
            return AxisResult.Error<T>(AxisError.InternalServerError(ex.Message));
        }
    }

    public Task<AxisResult> RemoveAsync(string key)
    {
        return AxisResult.TryAsync(() =>
        {
            _cancellationToken.ThrowIfCancellationRequested();
            memoryCache.Remove(key);
            return Task.CompletedTask;
        });
    }

    public Task<AxisResult<bool>> ExistsAsync(string key)
    {
        return AxisResult.TryAsync(() =>
        {
            _cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(memoryCache.TryGetValue(key, out _));
        });
    }

}
