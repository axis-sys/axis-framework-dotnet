using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AxisStorage.AzureBlob.UnitTests.AzureBlob;

public class DependencyInjectionTests
{
    private static AzureBlobCredentialSettings CreateCredentialSettings() => new();

    private static AzureBlobSettings CreateStorageSettings() => new()
    {
        AccountUrl = "https://test.blob.core.windows.net",
        Container = "test-container"
    };

    private static IServiceCollection CreateBuilderWithAccessor()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IAxisMediatorAccessor>().Object);
        return services;
    }

    [Fact]
    public void AddAxisAzureBlobStorage_ShouldRegisterIAxisStorageAsAzureBlobStorageAdapter()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisAzureBlobStorage(CreateCredentialSettings(), CreateStorageSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var storage = serviceProvider.GetService<IAxisStorage>();
        Assert.NotNull(storage);
        Assert.IsType<AzureBlobStorageAdapter>(storage);
    }

    [Fact]
    public void AddAxisAzureBlobStorage_ShouldRegisterStorageAsSingleton()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisAzureBlobStorage(CreateCredentialSettings(), CreateStorageSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var instance1 = serviceProvider.GetService<IAxisStorage>();
        var instance2 = serviceProvider.GetService<IAxisStorage>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void AddAxisAzureBlobStorage_ShouldRegisterSettingsAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var settings = CreateStorageSettings();

        // Act
        services.AddAxisAzureBlobStorage(CreateCredentialSettings(), settings);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var resolved = serviceProvider.GetService<AzureBlobSettings>();
        Assert.NotNull(resolved);
        Assert.Same(settings, resolved);
    }

    [Fact]
    public void AddAxisAzureBlobStorage_ShouldRegisterIAxisStorageContainerAsSameInstanceAsIAxisStorage()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisAzureBlobStorage(CreateCredentialSettings(), CreateStorageSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var storage = serviceProvider.GetService<IAxisStorage>();
        var container = serviceProvider.GetService<IAxisStorageContainer>();
        Assert.NotNull(container);
        Assert.Same(storage, container);
    }

    [Fact]
    public void AddAxisAzureBlobStorage_ShouldRegisterIAxisStorageListerAsSameInstanceAsIAxisStorage()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisAzureBlobStorage(CreateCredentialSettings(), CreateStorageSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var storage = serviceProvider.GetService<IAxisStorage>();
        var lister = serviceProvider.GetService<IAxisStorageLister>();
        Assert.NotNull(lister);
        Assert.Same(storage, lister);
    }

    [Fact]
    public void AddAxisAzureBlobStorage_ShouldRegisterIAxisStorageUrlResolverAsSameInstanceAsIAxisStorage()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisAzureBlobStorage(CreateCredentialSettings(), CreateStorageSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var storage = serviceProvider.GetService<IAxisStorage>();
        var urlResolver = serviceProvider.GetService<IAxisStorageUrlResolver>();
        Assert.NotNull(urlResolver);
        Assert.Same(storage, urlResolver);
    }

    [Fact]
    public void AddAxisAzureBlobStorage_ShouldReturnBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAxisAzureBlobStorage(CreateCredentialSettings(), CreateStorageSettings());

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddAxisAzureBlobStorageFactory_ShouldRegisterFactoryAsSingleton()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisAzureBlobStorageFactory(CreateCredentialSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var first = serviceProvider.GetService<IAzureBlobStorageFactory>();
        var second = serviceProvider.GetService<IAzureBlobStorageFactory>();
        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void AddAxisAzureBlobStorageFactory_ShouldApplyConfiguredOptions()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisAzureBlobStorageFactory(CreateCredentialSettings(), o => o.PublicAccessCacheTtl = TimeSpan.FromMinutes(1));
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<AzureBlobStorageOptions>();
        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromMinutes(1), options.PublicAccessCacheTtl);
    }
}
