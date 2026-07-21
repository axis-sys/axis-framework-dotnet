using Amazon.S3;
using Amazon.S3.Model;
using AxisMediator.Contracts;
using System.Net;

namespace AxisStorage.CloudflareR2.UnitTests.CloudflareR2;

public class CloudflareR2StorageAdapterTests
{
    private readonly Mock<IAmazonS3> _s3Mock;
    private readonly Mock<IAxisMediatorAccessor> _accessor;
    private readonly CloudflareR2StorageAdapter _adapter;
    private readonly IAxisMediator _defaultCancellationToken;
    private readonly IAxisMediator _canceledToken;

    public CloudflareR2StorageAdapterTests()
    {
        var defaultCancellationMock = new Mock<IAxisMediator>();
        defaultCancellationMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        _defaultCancellationToken = defaultCancellationMock.Object;

        var canceledMock = new Mock<IAxisMediator>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        canceledMock.SetupGet(x => x.CancellationToken).Returns(cts.Token);
        _canceledToken = canceledMock.Object;

        _s3Mock = new Mock<IAmazonS3>();
        _accessor = new Mock<IAxisMediatorAccessor>();
        var settings = new CloudflareR2Settings
        {
            AccountId = "test-account",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            BucketName = "test-bucket"
        };
        _adapter = new CloudflareR2StorageAdapter(_accessor.Object, _s3Mock.Object, settings);
    }

    #region UploadAsync

    [Fact]
    public async Task UploadAsync_ShouldReturnSuccess_WhenUploadSucceeds()
    {
        // Arrange
        _s3Mock.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var result = await _adapter.UploadAsync("files/test.txt", stream, "text/plain");

        // Assert
        result.ShouldSucceed();
        _s3Mock.Verify(s => s.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "files/test.txt" &&
                r.ContentType == "text/plain"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_ShouldReturnFailure_WhenS3Throws()
    {
        // Arrange
        _s3Mock.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Upload failed"));
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
        var expectedStream = new MemoryStream([1, 2, 3]);
        var response = new GetObjectResponse { ResponseStream = expectedStream };

        _s3Mock.Setup(s => s.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.DownloadAsync("files/test.txt");

        // Assert
        Assert.Same(expectedStream, result.ShouldSucceed());
        _s3Mock.Verify(s => s.GetObjectAsync(
            It.Is<GetObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "files/test.txt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_ShouldReturnNotFound_WhenKeyDoesNotExist()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.DownloadAsync("files/missing.txt");

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task DownloadAsync_ShouldReturnFailure_WhenS3Throws()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Server error") { StatusCode = HttpStatusCode.InternalServerError });
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.DownloadAsync("files/test.txt");

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task DownloadAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(()
            => _adapter.DownloadAsync("files/test.txt"));
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ShouldReturnSuccess_WhenDeleteSucceeds()
    {
        // Arrange
        _s3Mock.Setup(s => s.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.DeleteAsync("files/test.txt");

        // Assert
        result.ShouldSucceed();
        _s3Mock.Verify(s => s.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "files/test.txt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFailure_WhenS3Throws()
    {
        // Arrange
        _s3Mock.Setup(s => s.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Delete failed"));
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
        await Assert.ThrowsAsync<OperationCanceledException>(()
            => _adapter.DeleteAsync("files/test.txt"));
    }

    #endregion

    #region ExistsAsync

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse());
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
        _s3Mock.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ExistsAsync("files/missing.txt");

        // Assert
        result.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFailure_WhenS3Throws()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Server error") { StatusCode = HttpStatusCode.InternalServerError });
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ExistsAsync("files/test.txt");

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task ExistsAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(()
            => _adapter.ExistsAsync("files/test.txt"));
    }

    [Fact]
    public async Task ExistsAsync_ShouldUseBucketNameFromSettings()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse());
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        await _adapter.ExistsAsync("files/test.txt");

        // Assert
        _s3Mock.Verify(s => s.GetObjectMetadataAsync(
            It.Is<GetObjectMetadataRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "files/test.txt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetPresignedUrlAsync

    [Fact]
    public async Task GetPresignedUrlAsync_ShouldReturnUrl_WhenSuccessful()
    {
        // Arrange
        const string expectedUrl = "https://test-bucket.r2.cloudflarestorage.com/files/test.txt?signature=abc";
        _s3Mock.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .ReturnsAsync(expectedUrl);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.GetPresignedUrlAsync("files/test.txt", TimeSpan.FromHours(1));

        // Assert
        result.ShouldSucceedWith(expectedUrl);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ShouldPassCorrectParameters()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .ReturnsAsync("https://url");
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        await _adapter.GetPresignedUrlAsync("files/test.txt", TimeSpan.FromMinutes(30));

        // Assert
        _s3Mock.Verify(s => s.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == "files/test.txt" &&
                r.Verb == HttpVerb.GET)), Times.Once);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ShouldReturnFailure_WhenS3Throws()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .ThrowsAsync(new AmazonS3Exception("Presign failed"));
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

    [Fact]
    public async Task GetServableUrlAsync_ShouldReturnRawPublicUrl_WhenPublicUrlConfigured()
    {
        // Arrange
        var settings = new CloudflareR2Settings
        {
            AccountId = "test-account",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            BucketName = "test-bucket",
            PublicUrl = "https://cdn.example.com"
        };
        var adapter = new CloudflareR2StorageAdapter(_accessor.Object, _s3Mock.Object, settings);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await adapter.GetServableUrlAsync("files/test.txt", TimeSpan.FromHours(1));

        // Assert
        result.ShouldSucceed();
        Assert.True(result.Value.IsPublic);
        Assert.Null(result.Value.ExpiresAt);
        Assert.Equal("https://cdn.example.com/files/test.txt", result.Value.Url);
    }

    [Fact]
    public async Task GetServableUrlAsync_ShouldReturnSignedUrl_WhenPublicUrlNotConfigured()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>())).ReturnsAsync("https://signed");
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.GetServableUrlAsync("files/test.txt", TimeSpan.FromHours(1));

        // Assert
        result.ShouldSucceed();
        Assert.False(result.Value.IsPublic);
        Assert.NotNull(result.Value.ExpiresAt);
        Assert.Equal("https://signed", result.Value.Url);
    }

    #endregion

    #region Container ExistsAsync

    [Fact]
    public async Task ContainerExistsAsync_ShouldReturnTrue_WhenBucketExists()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetBucketLocationAsync(It.IsAny<GetBucketLocationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetBucketLocationResponse());
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ExistsAsync();

        // Assert
        result.ShouldSucceedWith(true);
        _s3Mock.Verify(s => s.GetBucketLocationAsync(
            It.Is<GetBucketLocationRequest>(r => r.BucketName == "test-bucket"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ContainerExistsAsync_ShouldReturnFalse_WhenBucketDoesNotExist()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetBucketLocationAsync(It.IsAny<GetBucketLocationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ExistsAsync();

        // Assert
        result.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task ContainerExistsAsync_ShouldReturnFailure_WhenS3Throws()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetBucketLocationAsync(It.IsAny<GetBucketLocationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Server error") { StatusCode = HttpStatusCode.InternalServerError });
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ExistsAsync();

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task ContainerExistsAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _adapter.ExistsAsync());
    }

    #endregion

    #region EnsureExistsAsync

    [Fact]
    public async Task EnsureExistsAsync_ShouldReturnSuccess_WhenBucketIsCreated()
    {
        // Arrange
        _s3Mock.Setup(s => s.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutBucketResponse());
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.EnsureExistsAsync();

        // Assert
        result.ShouldSucceed();
        _s3Mock.Verify(s => s.PutBucketAsync(
            It.Is<PutBucketRequest>(r => r.BucketName == "test-bucket"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldReturnSuccess_WhenBucketAlreadyOwnedByCaller()
    {
        // Arrange
        _s3Mock.Setup(s => s.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Already owned") { ErrorCode = "BucketAlreadyOwnedByYou" });
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.EnsureExistsAsync();

        // Assert
        result.ShouldSucceed();
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldReturnFailure_WhenS3Throws()
    {
        // Arrange
        _s3Mock.Setup(s => s.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Denied") { ErrorCode = "AccessDenied" });
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
    public async Task IsPubliclyAccessibleAsync_ShouldReturnTrue_WhenAllUsersHaveReadGrant()
    {
        // Arrange
        var response = new GetBucketAclResponse
        {
            Grants =
            [
                new S3Grant
                {
                    Grantee = new S3Grantee { URI = "http://acs.amazonaws.com/groups/global/AllUsers" },
                    Permission = S3Permission.READ
                }
            ]
        };
        _s3Mock.Setup(s => s.GetBucketAclAsync(It.IsAny<GetBucketAclRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.IsPubliclyAccessibleAsync();

        // Assert
        result.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task IsPubliclyAccessibleAsync_ShouldReturnFalse_WhenNoPublicGrant()
    {
        // Arrange
        var response = new GetBucketAclResponse { Grants = [] };
        _s3Mock.Setup(s => s.GetBucketAclAsync(It.IsAny<GetBucketAclRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.IsPubliclyAccessibleAsync();

        // Assert
        result.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task IsPubliclyAccessibleAsync_ShouldReturnFailure_WhenS3Throws()
    {
        // Arrange
        _s3Mock.Setup(s => s.GetBucketAclAsync(It.IsAny<GetBucketAclRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Server error"));
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.IsPubliclyAccessibleAsync();

        // Assert
        result.ShouldFail();
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
    public async Task ListAsync_ShouldReturnKeys_WhenSinglePage()
    {
        // Arrange
        var response = new ListObjectsV2Response
        {
            S3Objects = [new S3Object { Key = "pages/1.tif" }, new S3Object { Key = "pages/2.tif" }],
            IsTruncated = false
        };
        _s3Mock.Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ListAsync("pages/");

        // Assert
        result.ShouldSucceedWith(["pages/1.tif", "pages/2.tif"]);
        _s3Mock.Verify(s => s.ListObjectsV2Async(
            It.Is<ListObjectsV2Request>(r => r.BucketName == "test-bucket" && r.Prefix == "pages/"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_ShouldPaginate_WhenTruncated()
    {
        // Arrange
        var firstPage = new ListObjectsV2Response
        {
            S3Objects = [new S3Object { Key = "pages/1.tif" }],
            IsTruncated = true,
            NextContinuationToken = "token-1"
        };
        var secondPage = new ListObjectsV2Response
        {
            S3Objects = [new S3Object { Key = "pages/2.tif" }],
            IsTruncated = false
        };
        _s3Mock.SetupSequence(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage)
            .ReturnsAsync(secondPage);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ListAsync("pages/");

        // Assert
        result.ShouldSucceedWith(["pages/1.tif", "pages/2.tif"]);
        _s3Mock.Verify(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ListAsync_ShouldReturnEmpty_WhenS3ObjectsIsNull()
    {
        // Arrange: S3-compatible providers (e.g. MinIO) return a null S3Objects collection,
        // not an empty one, when no key matches the prefix.
        var response = new ListObjectsV2Response { S3Objects = null!, IsTruncated = false };
        _s3Mock.Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _accessor.SetupGet(x => x.AxisMediator).Returns(_defaultCancellationToken);

        // Act
        var result = await _adapter.ListAsync("pages/");

        // Assert
        Assert.Empty(result.ShouldSucceed());
    }

    [Fact]
    public async Task ListAsync_ShouldReturnFailure_WhenS3Throws()
    {
        // Arrange
        _s3Mock.Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Server error"));
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
