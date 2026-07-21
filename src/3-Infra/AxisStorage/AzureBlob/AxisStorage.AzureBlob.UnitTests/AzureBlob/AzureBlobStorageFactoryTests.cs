using AxisMediator.Contracts;
using Azure.Core;
using Azure.Storage.Blobs;

namespace AxisStorage.AzureBlob.UnitTests.AzureBlob;

public class AzureBlobStorageFactoryTests
{
    private static AzureBlobStorageFactory CreateFactory()
        => new(
            accountUrl => new BlobServiceClient(new Uri(accountUrl), new Mock<TokenCredential>().Object),
            new Mock<IAxisMediatorAccessor>().Object,
            new AzureBlobStorageOptions());

    private static AzureBlobSettings Destination(string account, string container)
        => new() { AccountUrl = account, Container = container };

    [Fact]
    public void Create_ShouldReturnSameInstance_ForSameDestination()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var first = factory.Create(Destination("https://acct.blob.core.windows.net", "c1"));
        var second = factory.Create(Destination("https://acct.blob.core.windows.net", "c1"));

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void Create_ShouldReturnDistinctInstances_ForDifferentDestinations()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var a = factory.Create(Destination("https://acct.blob.core.windows.net", "c1"));
        var b = factory.Create(Destination("https://acct.blob.core.windows.net", "c2"));

        // Assert
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Create_ShouldReturnInstanceImplementingAllCapabilities()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var storage = factory.Create(Destination("https://acct.blob.core.windows.net", "c1"));

        // Assert
        Assert.IsAssignableFrom<IAxisStorage>(storage);
        Assert.IsAssignableFrom<IAxisStorageContainer>(storage);
        Assert.IsAssignableFrom<IAxisStorageLister>(storage);
        Assert.IsAssignableFrom<IAxisStorageUrlResolver>(storage);
    }
}
