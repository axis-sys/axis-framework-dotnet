namespace Axis;

public interface IAxisStorageLister
{
    Task<AxisResult<IReadOnlyList<string>>> ListAsync(string prefix);
}
