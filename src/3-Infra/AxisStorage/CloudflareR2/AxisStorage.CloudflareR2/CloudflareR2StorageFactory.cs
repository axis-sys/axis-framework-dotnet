using System.Collections.Concurrent;
using Amazon;
using Amazon.S3;
using Axis;
using AxisMediator.Contracts;

namespace AxisStorage.CloudflareR2;

internal sealed class CloudflareR2StorageFactory(IAxisMediatorAccessor accessor) : ICloudflareR2StorageFactory
{
    private readonly ConcurrentDictionary<string, CloudflareR2StorageAdapter> _adapters = new();

    public IAxisStorage Create(CloudflareR2Settings destination)
        => _adapters.GetOrAdd($"{destination.AccountId}/{destination.BucketName}", _ =>
        {
            var s3Client = new AmazonS3Client(destination.AccessKey, destination.SecretKey, new AmazonS3Config
            {
                ServiceURL = destination.ServiceUrl,
                AuthenticationRegion = RegionEndpoint.USEast1.SystemName,
                ForcePathStyle = true
            });
            return new CloudflareR2StorageAdapter(accessor, s3Client, destination);
        });
}
