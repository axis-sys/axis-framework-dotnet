using AxisMediator.Contracts;
using System.Text;

namespace AxisStorage.FileSystem.UnitTests;

public sealed class FileSystemStorageAdapterTests : IDisposable
{
    private readonly string _root;
    private readonly Mock<IAxisMediatorAccessor> _accessor = new();
    private readonly FileSystemStorageAdapter _adapter;
    private readonly IAxisMediator _canceledToken;

    public FileSystemStorageAdapterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "axisfs-tests", Guid.NewGuid().ToString("N"));

        var defaultMock = new Mock<IAxisMediator>();
        defaultMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        _accessor.SetupGet(x => x.AxisMediator).Returns(defaultMock.Object);

        var canceledMock = new Mock<IAxisMediator>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        canceledMock.SetupGet(x => x.CancellationToken).Returns(cts.Token);
        _canceledToken = canceledMock.Object;

        _adapter = new FileSystemStorageAdapter(new FileSystemStorageSettings { Root = _root }, _accessor.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private async Task SeedAsync(string key, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = await _adapter.UploadAsync(key, stream, "text/plain");
        result.ShouldSucceed();
    }

    [Fact]
    public async Task UploadAsync_ShouldPersistContent_AndBeRetrievable()
    {
        // Arrange
        await SeedAsync("pages/1.txt", "hello disk");

        // Act
        var download = await _adapter.DownloadAsync("pages/1.txt");

        // Assert
        download.ShouldSucceed();
        using var reader = new StreamReader(download.Value);
        Assert.Equal("hello disk", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UploadAsync_ShouldOverwrite_WhenKeyExists()
    {
        // Arrange
        await SeedAsync("pages/1.txt", "first");

        // Act
        await SeedAsync("pages/1.txt", "second");

        // Assert
        var download = await _adapter.DownloadAsync("pages/1.txt");
        using var reader = new StreamReader(download.Value);
        Assert.Equal("second", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UploadAsync_ShouldThrow_WhenCancelled()
    {
        // Arrange
        _accessor.SetupGet(x => x.AxisMediator).Returns(_canceledToken);
        using var stream = new MemoryStream([1, 2, 3]);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _adapter.UploadAsync("pages/1.txt", stream, "text/plain"));
    }

    [Fact]
    public async Task DownloadAsync_ShouldReturnFailure_WhenKeyDoesNotExist()
    {
        // Act
        var result = await _adapter.DownloadAsync("missing/x.txt");

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveObject_AndBeIdempotent()
    {
        // Arrange
        await SeedAsync("pages/1.txt", "data");

        // Act
        var first = await _adapter.DeleteAsync("pages/1.txt");
        var second = await _adapter.DeleteAsync("pages/1.txt");

        // Assert
        first.ShouldSucceed();
        second.ShouldSucceed();
        var exists = await _adapter.ExistsAsync("pages/1.txt");
        exists.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        // Act
        var result = await _adapter.ExistsAsync("missing/x.txt");

        // Assert
        result.ShouldSucceedWith(false);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ShouldReturnFileUri()
    {
        // Act
        var result = await _adapter.GetPresignedUrlAsync("pages/1.txt", TimeSpan.FromMinutes(5));

        // Assert
        result.ShouldSucceed();
        Assert.StartsWith("file://", result.Value);
    }

    [Fact]
    public async Task ContainerExistsAsync_ShouldReflectDirectoryPresence()
    {
        // Act & Assert (root not created yet)
        var before = await _adapter.ExistsAsync();
        before.ShouldSucceedWith(false);

        var ensure = await _adapter.EnsureExistsAsync();
        ensure.ShouldSucceed();

        var after = await _adapter.ExistsAsync();
        after.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task IsPubliclyAccessibleAsync_ShouldReturnTrue()
    {
        // Act
        var result = await _adapter.IsPubliclyAccessibleAsync();

        // Assert
        result.ShouldSucceedWith(true);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnKeysUnderPrefix()
    {
        // Arrange
        await SeedAsync("pages/1.txt", "a");
        await SeedAsync("pages/2.txt", "b");
        await SeedAsync("other/3.txt", "c");

        // Act
        var result = await _adapter.ListAsync("pages/");

        // Assert
        result.ShouldSucceed();
        Assert.Equal(["pages/1.txt", "pages/2.txt"], result.Value.OrderBy(k => k));
    }

    [Fact]
    public async Task ListAsync_ShouldReturnEmpty_WhenRootMissing()
    {
        // Act
        var result = await _adapter.ListAsync("pages/");

        // Assert
        result.ShouldSucceed();
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Operations_ShouldNotThrow_WhenAmbientMediatorIsNull()
    {
        // Arrange — background context (e.g. a reconciler) has no ambient mediator
        var accessor = new Mock<IAxisMediatorAccessor>();
        accessor.SetupGet(x => x.AxisMediator).Returns((IAxisMediator?)null);
        var adapter = new FileSystemStorageAdapter(new FileSystemStorageSettings { Root = _root }, accessor.Object);

        // Act
        using var stream = new MemoryStream([1, 2, 3]);
        var upload = await adapter.UploadAsync("pages/1.txt", stream, "text/plain");
        var delete = await adapter.DeleteAsync("pages/1.txt");

        // Assert
        upload.ShouldSucceed();
        delete.ShouldSucceed();
    }
}
