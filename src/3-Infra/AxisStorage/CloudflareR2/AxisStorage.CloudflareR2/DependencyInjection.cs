using Amazon;
using Amazon.S3;
using Axis;
using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AxisStorage.CloudflareR2;

public static class DependencyInjection
{
    public static IServiceCollection AddAxisCloudflareR2Storage(this IServiceCollection services, CloudflareR2Settings settings)
    {
        var s3Client = new AmazonS3Client(
            settings.AccessKey,
            settings.SecretKey,
            new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl,
                AuthenticationRegion = RegionEndpoint.USEast1.SystemName,
                ForcePathStyle = true
            });

        services.AddSingleton(settings);
        services.AddSingleton<IAmazonS3>(s3Client);
        services.AddSingleton<CloudflareR2StorageAdapter>();
        services.AddSingleton<IAxisStorage>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
        services.AddSingleton<IAxisStorageContainer>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
        services.AddSingleton<IAxisStorageLister>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
        services.AddSingleton<IAxisStorageUrlResolver>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
        return services;
    }

    public static IServiceCollection AddAxisCloudflareR2StorageFactory(this IServiceCollection services)
    {
        services.AddSingleton<ICloudflareR2StorageFactory>(sp => new CloudflareR2StorageFactory(
            sp.GetRequiredService<IAxisMediatorAccessor>()));
        return services;
    }
}
