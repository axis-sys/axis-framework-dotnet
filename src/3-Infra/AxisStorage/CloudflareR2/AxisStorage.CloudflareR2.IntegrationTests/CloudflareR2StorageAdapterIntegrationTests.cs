using Amazon.S3;
using AxisMediator.Contracts;
using Moq;

namespace AxisStorage.CloudflareR2.IntegrationTests;

[Collection("MinioCollection")]
public class CloudflareR2StorageAdapterIntegrationTests(MinioFixture fixture) : IDisposable
{
    private readonly IAmazonS3 _s3Client = fixture.CreateS3Client();
    private readonly CloudflareR2StorageAdapter _adapter = CreateAdapter(fixture);

    private static CloudflareR2StorageAdapter CreateAdapter(MinioFixture fixture)
    {
        var s3Client = fixture.CreateS3Client();
        var accessorMock = new Mock<IAxisMediatorAccessor>();
        var mediatorMock = new Mock<IAxisMediator>();
        mediatorMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        accessorMock.SetupGet(x => x.AxisMediator).Returns(mediatorMock.Object);
        return new CloudflareR2StorageAdapter(accessorMock.Object, s3Client, fixture.Settings);
    }

    private static CloudflareR2StorageAdapter CreateAdapterForBucket(MinioFixture fixture, string bucketName)
    {
        var s3Client = fixture.CreateS3Client();
        var accessorMock = new Mock<IAxisMediatorAccessor>();
        var mediatorMock = new Mock<IAxisMediator>();
        mediatorMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        accessorMock.SetupGet(x => x.AxisMediator).Returns(mediatorMock.Object);
        var settings = new CloudflareR2Settings
        {
            AccountId = fixture.Settings.AccountId,
            AccessKey = fixture.Settings.AccessKey,
            SecretKey = fixture.Settings.SecretKey,
            BucketName = bucketName
        };
        return new CloudflareR2StorageAdapter(accessorMock.Object, s3Client, settings);
    }

    public void Dispose() => _s3Client.Dispose();

    private static string UniqueKey(string prefix = "test") => $"{prefix}/{Guid.NewGuid():N}.txt";

    #region UploadAsync

    [Fact]
    public async Task UploadAsync_ShouldUploadContent_AndBeRetrievable()
    {
        // Arrange
        var key = UniqueKey();
        var content = "Hello, R2!"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var result = await _adapter.UploadAsync(key, stream, "text/plain");

        // Assert
        result.ShouldSucceed();

        var response = await _s3Client.GetObjectAsync(fixture.Settings.BucketName, key, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(response.ResponseStream);
        var retrieved = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Hello, R2!", retrieved);
    }

    [Fact]
    public async Task UploadAsync_ShouldPreserveContentType()
    {
        // Arrange
        var key = UniqueKey();
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var result = await _adapter.UploadAsync(key, stream, "application/octet-stream");

        // Assert
        result.ShouldSucceed();

        var metadata = await _s3Client.GetObjectMetadataAsync(fixture.Settings.BucketName, key, TestContext.Current.CancellationToken);
        Assert.Equal("application/octet-stream", metadata.Headers.ContentType);
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

        var response = await _s3Client.GetObjectAsync(fixture.Settings.BucketName, key, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(response.ResponseStream);
        Assert.Equal("second", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    #endregion

    #region DownloadAsync

    [Fact]
    public async Task DownloadAsync_ShouldReturnStream_WhenKeyExists()
    {
        // Arrange
        var key = UniqueKey();
        var content = "download me"u8.ToArray();
        using var uploadStream = new MemoryStream(content);
        await _adapter.UploadAsync(key, uploadStream, "text/plain");

        // Act
        var result = await _adapter.DownloadAsync(key);

        // Assert
        result.ShouldSucceed();

        using var reader = new StreamReader(result.Value);
        Assert.Equal("download me", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

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
        // S3 DeleteObject is idempotent
        var result = await _adapter.DeleteAsync(UniqueKey("nonexistent"));

        result.ShouldSucceed();
    }

    #endregion

    #region ExistsAsync

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

    #region GetPresignedUrlAsync

    [Fact]
    public async Task GetPresignedUrlAsync_ShouldReturnUrl_WhenKeyExists()
    {
        // Arrange
        var key = UniqueKey();
        using var stream = new MemoryStream([1, 2, 3]);
        await _adapter.UploadAsync(key, stream, "text/plain");

        // Act
        var result = await _adapter.GetPresignedUrlAsync(key, TimeSpan.FromMinutes(5));

        // Assert
        result.ShouldSucceed();
        Assert.Contains(key, result.Value);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ShouldReturnUrl_EvenWhenKeyDoesNotExist()
    {
        // S3 presigned URLs are generated client-side, no server check
        var result = await _adapter.GetPresignedUrlAsync(UniqueKey("future"), TimeSpan.FromMinutes(5));

        result.ShouldSucceed();
        Assert.False(string.IsNullOrWhiteSpace(result.Value));
    }

    #endregion

    #region Full Lifecycle

    [Fact]
    public async Task FullLifecycle_UploadExistsDownloadDeleteExists()
    {
        // Arrange
        var key = UniqueKey("lifecycle");
        var content = "lifecycle test"u8.ToArray();

        // Upload
        using var uploadStream = new MemoryStream(content);
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

        // Presigned URL
        var urlResult = await _adapter.GetPresignedUrlAsync(key, TimeSpan.FromMinutes(1));
        urlResult.ShouldSucceed();

        // Delete
        var deleteResult = await _adapter.DeleteAsync(key);
        deleteResult.ShouldSucceed();

        // Exists (false)
        var existsAfterDelete = await _adapter.ExistsAsync(key);
        existsAfterDelete.ShouldSucceedWith(false);
    }

    #endregion

    #region Container ExistsAsync / EnsureExistsAsync

    [Fact]
    public async Task ContainerExistsAsync_ShouldReturnTrue_ForTheProvisionedBucket()
    {
        // Act
        var result = await _adapter.ExistsAsync();

        // Assert
        result.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task ContainerExistsAsync_ShouldReturnFalse_ForAnUnprovisionedBucket()
    {
        // Arrange
        var adapter = CreateAdapterForBucket(fixture, $"unprovisioned-{Guid.NewGuid():N}");

        // Act
        var result = await adapter.ExistsAsync();

        // Assert
        result.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldCreateBucket_WhenItDoesNotExistYet()
    {
        // Arrange
        var bucketName = $"ensure-{Guid.NewGuid():N}";
        var adapter = CreateAdapterForBucket(fixture, bucketName);

        // Act
        var result = await adapter.EnsureExistsAsync();

        // Assert
        result.ShouldSucceed();
        var exists = await adapter.ExistsAsync();
        exists.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldBeIdempotent_WhenBucketAlreadyExists()
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
    public async Task IsPubliclyAccessibleAsync_ShouldReturnFalse_ForAFreshlyProvisionedBucket()
    {
        // MinIO buckets are private by default; flipping a bucket to public requires a policy
        // change outside the scope of this adapter's contract, so only the default (private) case
        // is exercised here.
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
}
