using AxisMediator.Contracts;
using Moq;

namespace AxisStorage.AzureBlob.IntegrationTests;

[Collection("AzuriteCollection")]
public class AzureBlobStorageAdapterIntegrationTests(AzuriteFixture fixture)
{
    private readonly AzureBlobStorageAdapter _adapter = CreateAdapter(fixture, fixture.Settings);

    private static AzureBlobStorageAdapter CreateAdapter(AzuriteFixture fixture, AzureBlobSettings settings)
    {
        var accessorMock = new Mock<IAxisMediatorAccessor>();
        var mediatorMock = new Mock<IAxisMediator>();
        mediatorMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        accessorMock.SetupGet(x => x.AxisMediator).Returns(mediatorMock.Object);
        return new AzureBlobStorageAdapter(fixture.CreateServiceClient(), settings, accessorMock.Object, new AzureBlobStorageOptions());
    }

    private static string UniqueKey(string prefix = "test") => $"{prefix}/{Guid.NewGuid():N}.txt";

    #region UploadAsync

    [Fact]
    public async Task UploadAsync_ShouldUploadContent_AndBeRetrievable()
    {
        // Arrange
        var key = UniqueKey();
        using var stream = new MemoryStream("Hello, Azure!"u8.ToArray());

        // Act
        var result = await _adapter.UploadAsync(key, stream, "text/plain");

        // Assert
        result.ShouldSucceed();

        var downloadResult = await _adapter.DownloadAsync(key);
        using var reader = new StreamReader(downloadResult.Value);
        Assert.Equal("Hello, Azure!", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UploadAsync_ShouldOverwriteExistingKey()
    {
        // Arrange
        var key = UniqueKey();
        using var first = new MemoryStream("first"u8.ToArray());
        using var second = new MemoryStream("second"u8.ToArray());
        await _adapter.UploadAsync(key, first, "text/plain");

        // Act
        var result = await _adapter.UploadAsync(key, second, "text/plain");

        // Assert
        result.ShouldSucceed();

        var downloadResult = await _adapter.DownloadAsync(key);
        using var reader = new StreamReader(downloadResult.Value);
        Assert.Equal("second", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    #endregion

    #region DownloadAsync

    [Fact]
    public async Task DownloadAsync_ShouldReturnFailure_WhenKeyDoesNotExist()
    {
        // Act
        var result = await _adapter.DownloadAsync(UniqueKey("missing"));

        // Assert
        result.ShouldFail();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ShouldRemoveObject_WhenKeyExists()
    {
        // Arrange
        var key = UniqueKey();
        using var stream = new MemoryStream([1, 2, 3]);
        await _adapter.UploadAsync(key, stream, "text/plain");

        // Act
        var result = await _adapter.DeleteAsync(key);

        // Assert
        result.ShouldSucceed();
        var exists = await _adapter.ExistsAsync(key);
        exists.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnSuccess_WhenKeyDoesNotExist()
    {
        // Act: blob delete is idempotent
        var result = await _adapter.DeleteAsync(UniqueKey("nonexistent"));

        // Assert
        result.ShouldSucceed();
    }

    #endregion

    #region ExistsAsync (blob)

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        // Arrange
        var key = UniqueKey();
        using var stream = new MemoryStream([1, 2, 3]);
        await _adapter.UploadAsync(key, stream, "text/plain");

        // Act
        var result = await _adapter.ExistsAsync(key);

        // Assert
        result.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        // Act
        var result = await _adapter.ExistsAsync(UniqueKey("nonexistent"));

        // Assert
        result.ShouldSucceedWith(false);
    }

    #endregion

    #region Container ExistsAsync / EnsureExistsAsync

    [Fact]
    public async Task ContainerExistsAsync_ShouldReturnTrue_ForTheProvisionedContainer()
    {
        // Act
        var result = await _adapter.ExistsAsync();

        // Assert
        result.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task ContainerExistsAsync_ShouldReturnFalse_ForAnUnprovisionedContainer()
    {
        // Arrange
        var adapter = CreateAdapter(fixture, new AzureBlobSettings
        {
            AccountUrl = fixture.Settings.AccountUrl,
            Container = $"unprovisioned-{Guid.NewGuid():N}"
        });

        // Act
        var result = await adapter.ExistsAsync();

        // Assert
        result.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldCreateContainer_WhenItDoesNotExistYet()
    {
        // Arrange
        var adapter = CreateAdapter(fixture, new AzureBlobSettings
        {
            AccountUrl = fixture.Settings.AccountUrl,
            Container = $"ensure-{Guid.NewGuid():N}"
        });

        // Act
        var result = await adapter.EnsureExistsAsync();

        // Assert
        result.ShouldSucceed();
        var exists = await adapter.ExistsAsync();
        exists.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldBeIdempotent_WhenContainerAlreadyExists()
    {
        // Act
        var first = await _adapter.EnsureExistsAsync();
        var second = await _adapter.EnsureExistsAsync();

        // Assert
        first.ShouldSucceed();
        second.ShouldSucceed();
    }

    #endregion

    #region IsPubliclyAccessibleAsync

    [Fact]
    public async Task IsPubliclyAccessibleAsync_ShouldReturnFalse_ForAFreshlyProvisionedContainer()
    {
        // Azurite containers are private by default; flipping one to public requires a policy
        // change outside the scope of this adapter's contract, so only the default (private)
        // case is exercised here.
        var result = await _adapter.IsPubliclyAccessibleAsync();

        result.ShouldSucceedWith(false);
    }

    #endregion

    #region ListAsync

    [Fact]
    public async Task ListAsync_ShouldReturnKeysUnderPrefix()
    {
        // Arrange
        var prefix = $"listing/{Guid.NewGuid():N}";
        var keyA = $"{prefix}/a.txt";
        var keyB = $"{prefix}/b.txt";
        using (var streamA = new MemoryStream([1]))
            await _adapter.UploadAsync(keyA, streamA, "text/plain");
        using (var streamB = new MemoryStream([2]))
            await _adapter.UploadAsync(keyB, streamB, "text/plain");

        // Act
        var result = await _adapter.ListAsync(prefix);

        // Assert
        result.ShouldSucceed();
        Assert.Equal([keyA, keyB], result.Value.OrderBy(k => k));
    }

    [Fact]
    public async Task ListAsync_ShouldReturnEmpty_WhenNoKeysMatchPrefix()
    {
        // Act
        var result = await _adapter.ListAsync($"unused/{Guid.NewGuid():N}");

        // Assert
        result.ShouldSucceed();
        Assert.Empty(result.Value);
    }

    #endregion

    #region Full Lifecycle

    [Fact]
    public async Task FullLifecycle_UploadExistsDownloadDeleteExists()
    {
        // Arrange
        var key = UniqueKey("lifecycle");

        // Upload
        using var uploadStream = new MemoryStream("lifecycle test"u8.ToArray());
        var uploadResult = await _adapter.UploadAsync(key, uploadStream, "text/plain");
        uploadResult.ShouldSucceed();

        // Exists (true)
        var existsResult = await _adapter.ExistsAsync(key);
        existsResult.ShouldSucceedWith(true);

        // Download
        var downloadResult = await _adapter.DownloadAsync(key);
        downloadResult.ShouldSucceed();
        using var reader = new StreamReader(downloadResult.Value);
        Assert.Equal("lifecycle test", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));

        // Delete
        var deleteResult = await _adapter.DeleteAsync(key);
        deleteResult.ShouldSucceed();

        // Exists (false)
        var existsAfterDelete = await _adapter.ExistsAsync(key);
        existsAfterDelete.ShouldSucceedWith(false);
    }

    #endregion
}
