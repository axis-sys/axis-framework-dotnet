namespace Axis;

public interface IAxisStorageContainer
{
    Task<AxisResult<bool>> ExistsAsync();

    Task<AxisResult> EnsureExistsAsync();

    Task<AxisResult<bool>> IsPubliclyAccessibleAsync();
}
