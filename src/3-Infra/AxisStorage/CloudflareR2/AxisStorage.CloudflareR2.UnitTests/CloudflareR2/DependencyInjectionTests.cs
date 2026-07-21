using Amazon.S3;
using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AxisStorage.CloudflareR2.UnitTests.CloudflareR2;

public class DependencyInjectionTests
{
    private static CloudflareR2Settings CreateSettings() => new()
    {
        AccountId = "test-account",
        AccessKey = "test-access-key",
        SecretKey = "test-secret-key",
        BucketName = "test-bucket"
    };

    private static IServiceCollection CreateBuilderWithAccessor()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IAxisMediatorAccessor>().Object);
        return services;
    }

    [Fact]
    public void AddAxisCloudflareR2Storage_ShouldRegisterIAxisStorageAsCloudflareR2StorageAdapter()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisCloudflareR2Storage(CreateSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var storage = serviceProvider.GetService<IAxisStorage>();
        Assert.NotNull(storage);
        Assert.IsType<CloudflareR2StorageAdapter>(storage);
    }

    [Fact]
    public void AddAxisCloudflareR2Storage_ShouldRegisterIAmazonS3AsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAxisCloudflareR2Storage(CreateSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var s3Client1 = serviceProvider.GetService<IAmazonS3>();
        var s3Client2 = serviceProvider.GetService<IAmazonS3>();
        Assert.NotNull(s3Client1);
        Assert.Same(s3Client1, s3Client2);
    }

    [Fact]
    public void AddAxisCloudflareR2Storage_ShouldRegisterSettingsAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var settings = CreateSettings();

        // Act
        services.AddAxisCloudflareR2Storage(settings);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var resolved = serviceProvider.GetService<CloudflareR2Settings>();
        Assert.NotNull(resolved);
        Assert.Same(settings, resolved);
    }

    [Fact]
    public void AddAxisCloudflareR2Storage_ShouldRegisterStorageAsSingleton()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisCloudflareR2Storage(CreateSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var instance1 = serviceProvider.GetService<IAxisStorage>();
        var instance2 = serviceProvider.GetService<IAxisStorage>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void AddAxisCloudflareR2Storage_ShouldRegisterIAxisStorageContainerAsSameInstanceAsIAxisStorage()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisCloudflareR2Storage(CreateSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var storage = serviceProvider.GetService<IAxisStorage>();
        var container = serviceProvider.GetService<IAxisStorageContainer>();
        Assert.NotNull(container);
        Assert.Same(storage, container);
    }

    [Fact]
    public void AddAxisCloudflareR2Storage_ShouldRegisterIAxisStorageListerAsSameInstanceAsIAxisStorage()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisCloudflareR2Storage(CreateSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var storage = serviceProvider.GetService<IAxisStorage>();
        var lister = serviceProvider.GetService<IAxisStorageLister>();
        Assert.NotNull(lister);
        Assert.Same(storage, lister);
    }

    [Fact]
    public void AddAxisCloudflareR2Storage_ShouldReturnBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAxisCloudflareR2Storage(CreateSettings());

        // Assert
        Assert.Same(services, result);
    }
}
