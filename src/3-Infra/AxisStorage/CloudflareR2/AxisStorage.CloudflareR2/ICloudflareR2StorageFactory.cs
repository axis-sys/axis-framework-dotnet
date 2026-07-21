using Axis;

namespace AxisStorage.CloudflareR2;

public interface ICloudflareR2StorageFactory
{
    IAxisStorage Create(CloudflareR2Settings destination);
}
