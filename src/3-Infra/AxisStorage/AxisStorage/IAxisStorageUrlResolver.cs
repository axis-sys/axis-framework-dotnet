namespace Axis;

public interface IAxisStorageUrlResolver
{
    Task<AxisResult<AxisStorageUrl>> GetServableUrlAsync(string key, TimeSpan expiration);
}
