using AxisMediator.Contracts;

namespace AxisStorage.FileSystem.UnitTests;

public class FileSystemStorageFactoryTests
{
    private static FileSystemStorageFactory CreateFactory()
    {
        var accessor = new Mock<IAxisMediatorAccessor>();
        return new FileSystemStorageFactory(accessor.Object);
    }

    [Fact]
    public void Create_ShouldReturnSameInstance_ForSameRoot()
    {
        // Arrange
        var factory = CreateFactory();
        var destination = new FileSystemStorageSettings { Root = @"C:\data\tenant-a" };

        // Act
        var first = factory.Create(destination);
        var second = factory.Create(new FileSystemStorageSettings { Root = @"C:\data\tenant-a" });

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void Create_ShouldReturnDistinctInstances_ForDifferentRoots()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var a = factory.Create(new FileSystemStorageSettings { Root = @"C:\data\tenant-a" });
        var b = factory.Create(new FileSystemStorageSettings { Root = @"C:\data\tenant-b" });

        // Assert
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Create_ShouldReturnInstanceImplementingAllCapabilities()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var storage = factory.Create(new FileSystemStorageSettings { Root = @"C:\data\tenant-a" });

        // Assert
        Assert.IsAssignableFrom<IAxisStorage>(storage);
        Assert.IsAssignableFrom<IAxisStorageContainer>(storage);
        Assert.IsAssignableFrom<IAxisStorageLister>(storage);
    }
}
