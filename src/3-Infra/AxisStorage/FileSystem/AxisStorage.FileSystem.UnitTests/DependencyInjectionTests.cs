using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AxisStorage.FileSystem.UnitTests;

public class DependencyInjectionTests
{
    private static FileSystemStorageSettings CreateSettings() => new() { Root = @"C:\data\pages" };

    private static IServiceCollection CreateBuilderWithAccessor()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IAxisMediatorAccessor>().Object);
        return services;
    }

    [Fact]
    public void AddAxisFileSystemStorage_ShouldRegisterIAxisStorageAsFileSystemStorageAdapter()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisFileSystemStorage(CreateSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var storage = serviceProvider.GetService<IAxisStorage>();
        Assert.NotNull(storage);
        Assert.IsType<FileSystemStorageAdapter>(storage);
    }

    [Fact]
    public void AddAxisFileSystemStorage_ShouldRegisterContainerAndListerAsSameInstance()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisFileSystemStorage(CreateSettings());
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var storage = serviceProvider.GetService<IAxisStorage>();
        var container = serviceProvider.GetService<IAxisStorageContainer>();
        var lister = serviceProvider.GetService<IAxisStorageLister>();
        Assert.Same(storage, container);
        Assert.Same(storage, lister);
    }

    [Fact]
    public void AddAxisFileSystemStorageFactory_ShouldRegisterFactoryAsSingleton()
    {
        // Arrange
        var services = CreateBuilderWithAccessor();

        // Act
        services.AddAxisFileSystemStorageFactory();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var first = serviceProvider.GetService<IFileSystemStorageFactory>();
        var second = serviceProvider.GetService<IFileSystemStorageFactory>();
        Assert.NotNull(first);
        Assert.Same(first, second);
    }
}
