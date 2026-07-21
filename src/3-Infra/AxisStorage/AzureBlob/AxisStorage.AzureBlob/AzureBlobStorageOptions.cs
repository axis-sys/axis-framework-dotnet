namespace AxisStorage.AzureBlob;

public sealed class AzureBlobStorageOptions
{
    public TimeSpan PublicAccessCacheTtl { get; set; } = TimeSpan.FromMinutes(15);
}
