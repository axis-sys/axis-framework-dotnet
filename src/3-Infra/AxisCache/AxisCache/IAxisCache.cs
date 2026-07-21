namespace Axis;

public interface IAxisCache
{
    Task<AxisResult<T?>> GetAsync<T>(string key);

    Task<AxisResult> SetAsync<T>(string key, T value, TimeSpan? expiration = null);

    Task<AxisResult<T>> GetOrCreateAsync<T>(string key, Func<Task<AxisResult<T>>> factory, TimeSpan? expiration = null);

    Task<AxisResult> RemoveAsync(string key);

    Task<AxisResult<bool>> ExistsAsync(string key);
}
