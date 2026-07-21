using Axis;

namespace AxisStorage.AzureBlob;

public interface IAzureBlobStorageFactory
{
    IAxisStorage Create(AzureBlobSettings destination);
}
