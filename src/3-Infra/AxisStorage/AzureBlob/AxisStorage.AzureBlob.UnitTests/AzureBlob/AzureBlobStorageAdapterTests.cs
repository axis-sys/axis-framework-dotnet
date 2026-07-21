using AxisMediator.Contracts;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AxisStorage.AzureBlob.UnitTests.AzureBlob;

public class AzureBlobStorageAdapterTests
{
    private const string ContainerName = "test-container";

    private readonly Mock<BlobServiceClient> _serviceClientMock = new();
    private readonly Mock<BlobContainerClient> _containerClientMock = new();
    private readonly Mock<BlobClient> _blobClientMock = new();
    private readonly Mock<IAxisMediatorAccessor> _accessor = new();
    private readonly AzureBlobStorageAdapter _adapter;
    private readonly IAxisMediator _defaultCancellationToken;
    private readonly IAxisMediator _canceledToken;

    public AzureBlobStorageAdapterTests()
    {
        var defaultCancellationMock = new Mock<IAxisMediator>();
        defaultCancellationMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        _defaultCancellationToken = defaultCancellationMock.Object;

        var canceledMock = new Mock<IAxisMediator>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        canceledMock.SetupGet(x => x.CancellationToken).Returns(cts.Token);
        _canceledToken = canceledMock.Object;

        _blobClientMock.Setup(b => b.Uri).Returns(new Uri($"https://test.blob.core.windows.net/{ContainerName}/files/test.txt"));
        _containerClientMock.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(_blobClientMock.Object);
        _serviceClientMock.Setup(s => s.GetBlobContainerClient(ContainerName)).Returns(_containerClientMock.Object);
        _serviceClientMock.Setup(s => s.AccountName).Returns("testaccount");

        var settings = new AzureBlobSettings { AccountUrl = "https://test.blob.core.windows.net", Container = ContainerName };
        _adapter = new AzureBlobStorageAdapter(_serviceClientMock.Object, settings, _accessor.Object, new AzureBlobStorageOptions());
    }

    private static Response<T> MockResponse<T>(T value) => Response.FromValue(value, Mock.Of<Response>());

    #region UploadAsync

    [Fact]
    public async Task UploadAsync_ShouldReturnSuccess_WhenUploadSucceeds()
    {
        // Arrange
        _blobClientMock.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockResponse(BlobsModelFactory.BlobContentInfo(new ETag("etag"), DateTimeOffset.UtcNow, [], null, 1)));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var result = await _adapter.UploadAsync("files/test.txt", stream, "text/plain");

        // Assert
        result.ShouldSucceed();
        // ReSharper disable once AccessToDisposedClosure -- Verify runs synchronously above, before the outer `using` disposes it
        _blobClientMock.Verify(b => b.UploadAsync(
            stream,
            It.Is<BlobUploadOptions>(o => o.HttpHeaders!.ContentType == "text/plain"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_ShouldReturnFailure_WhenSdkThrows()
    {
        // Arrange
        _blobClientMock.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "Forbidden"));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var result = await _adapter.UploadAsync("files/test.txt", stream, "text/plain");

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task UploadAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        using var stream = new MemoryStream([1, 2, 3]);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(()
            => _adapter.UploadAsync("files/test.txt", stream, "text/plain"));
    }

    #endregion

    #region DownloadAsync

    [Fact]
    public async Task DownloadAsync_ShouldReturnStream_WhenKeyExists()
    {
        // Arrange
        var content = new MemoryStream([1, 2, 3]);
        var download = BlobsModelFactory.BlobDownloadStreamingResult(content);
        _blobClientMock.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockResponse(download));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.DownloadAsync("files/test.txt");

        // Assert
        Assert.Same(content, result.ShouldSucceed());
    }

    [Fact]
    public async Task DownloadAsync_ShouldReturnFailure_WhenSdkThrows()
    {
        // Arrange
        _blobClientMock.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.DownloadAsync("files/missing.txt");

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task DownloadAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _adapter.DownloadAsync("files/test.txt"));
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ShouldReturnSuccess_WhenDeleteSucceeds()
    {
        // Arrange
        _blobClientMock.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockResponse(true));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.DeleteAsync("files/test.txt");

        // Assert
        result.ShouldSucceed();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFailure_WhenSdkThrows()
    {
        // Arrange
        _blobClientMock.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "Forbidden"));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.DeleteAsync("files/test.txt");

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _adapter.DeleteAsync("files/test.txt"));
    }

    #endregion

    #region ExistsAsync (blob)

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        // Arrange
        _blobClientMock.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MockResponse(true));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ExistsAsync("files/test.txt");

        // Assert
        result.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        // Arrange
        _blobClientMock.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MockResponse(false));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ExistsAsync("files/missing.txt");

        // Assert
        result.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task ExistsAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _adapter.ExistsAsync("files/test.txt"));
    }

    #endregion

    #region GetPresignedUrlAsync

    [Fact]
    public async Task GetPresignedUrlAsync_ShouldReturnUrl_WhenSuccessful()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var delegationKey = BlobsModelFactory.UserDelegationKey(
            "signed-object-id", "signed-tenant-id", now, now.AddHours(1), "b", "2020-02-10", Convert.ToBase64String("test-delegation-key"u8.ToArray()));
        _serviceClientMock.Setup(s => s.GetUserDelegationKeyAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockResponse(delegationKey));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.GetPresignedUrlAsync("files/test.txt", TimeSpan.FromHours(1));

        // Assert
        Assert.StartsWith("https://test.blob.core.windows.net/test-container/files/test.txt?", result.ShouldSucceed());
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ShouldReturnFailure_WhenSdkThrows()
    {
        // Arrange
        _serviceClientMock.Setup(s => s.GetUserDelegationKeyAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "Forbidden"));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.GetPresignedUrlAsync("files/test.txt", TimeSpan.FromHours(1));

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(()
            => _adapter.GetPresignedUrlAsync("files/test.txt", TimeSpan.FromHours(1)));
    }

    #endregion

    #region GetServableUrlAsync

    private void SetupContainerPublicAccess(PublicAccessType publicAccess)
    {
        var properties = BlobsModelFactory.BlobContainerProperties(DateTimeOffset.UtcNow, new ETag("etag"), publicAccess: publicAccess);
        _containerClientMock.Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockResponse(properties));
    }

    private void SetupDelegationKey()
    {
        var now = DateTimeOffset.UtcNow;
        var delegationKey = BlobsModelFactory.UserDelegationKey(
            "signed-object-id", "signed-tenant-id", now, now.AddHours(1), "b", "2020-02-10", Convert.ToBase64String("test-delegation-key"u8.ToArray()));
        _serviceClientMock.Setup(s => s.GetUserDelegationKeyAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockResponse(delegationKey));
    }

    [Fact]
    public async Task GetServableUrlAsync_ShouldReturnRawPublicUrl_WhenContainerIsPublic()
    {
        // Arrange
        SetupContainerPublicAccess(PublicAccessType.BlobContainer);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.GetServableUrlAsync("files/test.txt", TimeSpan.FromHours(1));

        // Assert
        result.ShouldSucceed();
        Assert.True(result.Value.IsPublic);
        Assert.Null(result.Value.ExpiresAt);
        Assert.DoesNotContain("?", result.Value.Url);
    }

    [Fact]
    public async Task GetServableUrlAsync_ShouldReturnSignedUrl_WhenContainerIsPrivate()
    {
        // Arrange
        SetupContainerPublicAccess(PublicAccessType.None);
        SetupDelegationKey();
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.GetServableUrlAsync("files/test.txt", TimeSpan.FromHours(1));

        // Assert
        result.ShouldSucceed();
        Assert.False(result.Value.IsPublic);
        Assert.NotNull(result.Value.ExpiresAt);
        Assert.Contains("?", result.Value.Url);
    }

    [Fact]
    public async Task GetServableUrlAsync_ShouldProbePublicAccessOnlyOnce_AcrossCalls()
    {
        // Arrange
        SetupContainerPublicAccess(PublicAccessType.BlobContainer);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        await _adapter.GetServableUrlAsync("files/a.txt", TimeSpan.FromHours(1));
        await _adapter.GetServableUrlAsync("files/b.txt", TimeSpan.FromHours(1));

        // Assert
        _containerClientMock.Verify(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetServableUrlAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(()
            => _adapter.GetServableUrlAsync("files/test.txt", TimeSpan.FromHours(1)));
    }

    #endregion

    #region Container ExistsAsync / EnsureExistsAsync

    [Fact]
    public async Task ContainerExistsAsync_ShouldReturnTrue_WhenContainerExists()
    {
        // Arrange
        _containerClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MockResponse(true));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ExistsAsync();

        // Assert
        result.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task ContainerExistsAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _adapter.ExistsAsync());
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldReturnSuccess_WhenContainerIsCreated()
    {
        // Arrange
        _containerClientMock.Setup(c => c.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockResponse(BlobsModelFactory.BlobContainerInfo(new ETag("etag"), DateTimeOffset.UtcNow)));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.EnsureExistsAsync();

        // Assert
        result.ShouldSucceed();
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldReturnFailure_WhenSdkThrows()
    {
        // Arrange
        _containerClientMock.Setup(c => c.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "Forbidden"));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.EnsureExistsAsync();

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _adapter.EnsureExistsAsync());
    }

    #endregion

    #region IsPubliclyAccessibleAsync

    [Fact]
    public async Task IsPubliclyAccessibleAsync_ShouldReturnTrue_WhenPublicAccessIsSet()
    {
        // Arrange
        var properties = BlobsModelFactory.BlobContainerProperties(
            DateTimeOffset.UtcNow, new ETag("etag"), publicAccess: PublicAccessType.BlobContainer);
        _containerClientMock.Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockResponse(properties));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.IsPubliclyAccessibleAsync();

        // Assert
        result.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task IsPubliclyAccessibleAsync_ShouldReturnFalse_WhenContainerIsPrivate()
    {
        // Arrange
        var properties = BlobsModelFactory.BlobContainerProperties(
            DateTimeOffset.UtcNow, new ETag("etag"), publicAccess: PublicAccessType.None);
        _containerClientMock.Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockResponse(properties));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.IsPubliclyAccessibleAsync();

        // Assert
        result.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task IsPubliclyAccessibleAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _adapter.IsPubliclyAccessibleAsync());
    }

    #endregion

    #region ListAsync

    [Fact]
    public async Task ListAsync_ShouldReturnKeys_WhenBlobsExist()
    {
        // Arrange
        var items = new[]
        {
            BlobsModelFactory.BlobItem("pages/1.tif", false, BlobsModelFactory.BlobItemProperties(false)),
            BlobsModelFactory.BlobItem("pages/2.tif", false, BlobsModelFactory.BlobItemProperties(false))
        };
        var page = Page<BlobItem>.FromValues(items, null, Mock.Of<Response>());
        var pageable = AsyncPageable<BlobItem>.FromPages([page]);
        _containerClientMock.Setup(c => c.GetBlobsAsync(BlobTraits.None, BlobStates.None, "pages/", It.IsAny<CancellationToken>()))
            .Returns(pageable);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ListAsync("pages/");

        // Assert
        result.ShouldSucceedWith(["pages/1.tif", "pages/2.tif"]);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnEmpty_WhenNoBlobsMatchPrefix()
    {
        // Arrange
        var page = Page<BlobItem>.FromValues([], null, Mock.Of<Response>());
        var pageable = AsyncPageable<BlobItem>.FromPages([page]);
        _containerClientMock.Setup(c => c.GetBlobsAsync(BlobTraits.None, BlobStates.None, "unused/", It.IsAny<CancellationToken>()))
            .Returns(pageable);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ListAsync("unused/");

        // Assert
        Assert.Empty(result.ShouldSucceed());
    }

    [Fact]
    public async Task ListAsync_ShouldReturnFailure_WhenSdkThrows()
    {
        // Arrange
        _containerClientMock.Setup(c => c.GetBlobsAsync(BlobTraits.None, BlobStates.None, "pages/", It.IsAny<CancellationToken>()))
            .Throws(new RequestFailedException(403, "Forbidden"));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ListAsync("pages/");

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task ListAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _adapter.ListAsync("pages/"));
    }

    #endregion
}
