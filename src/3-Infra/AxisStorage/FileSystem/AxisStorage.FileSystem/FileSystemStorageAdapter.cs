using Axis;
using AxisMediator.Contracts;

namespace AxisStorage.FileSystem;

internal sealed class FileSystemStorageAdapter(
    FileSystemStorageSettings settings,
    IAxisMediatorAccessor accessor
) : IAxisStorage, IAxisStorageContainer, IAxisStorageLister
{
    public Task<AxisResult> UploadAsync(string key, Stream content, string contentType)
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            if (content.CanSeek)
                content.Position = 0;
            var path = PathFor(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var file = File.Create(path);
            await content.CopyToAsync(file, ct);
        });

    public Task<AxisResult<Stream>> DownloadAsync(string key)
        => AxisResult.TryAsync(() =>
        {
            CancellationToken();
            return Task.FromResult<Stream>(File.OpenRead(PathFor(key)));
        });

    public Task<AxisResult> DeleteAsync(string key)
        => AxisResult.TryAsync(() =>
        {
            CancellationToken();
            var path = PathFor(key);
            if (File.Exists(path))
                File.Delete(path);
            return Task.CompletedTask;
        });

    public Task<AxisResult<bool>> ExistsAsync(string key)
        => AxisResult.TryAsync(() =>
        {
            CancellationToken();
            return Task.FromResult(File.Exists(PathFor(key)));
        });

    public Task<AxisResult<string>> GetPresignedUrlAsync(string key, TimeSpan expiration)
        => AxisResult.TryAsync(() =>
        {
            CancellationToken();
            return Task.FromResult(new Uri(PathFor(key)).AbsoluteUri);
        });

    public Task<AxisResult<bool>> ExistsAsync()
        => AxisResult.TryAsync(() =>
        {
            CancellationToken();
            return Task.FromResult(Directory.Exists(settings.Root));
        });

    public Task<AxisResult> EnsureExistsAsync()
        => AxisResult.TryAsync(() =>
        {
            CancellationToken();
            Directory.CreateDirectory(settings.Root);
            return Task.CompletedTask;
        });

    public Task<AxisResult<bool>> IsPubliclyAccessibleAsync()
        => AxisResult.TryAsync(() =>
        {
            CancellationToken();
            return Task.FromResult(true);
        });

    public Task<AxisResult<IReadOnlyList<string>>> ListAsync(string prefix)
        => AxisResult.TryAsync(() =>
        {
            var ct = CancellationToken();
            if (!Directory.Exists(settings.Root))
                return Task.FromResult((IReadOnlyList<string>)[]);

            List<string> keys = [];
            foreach (var file in Directory.EnumerateFiles(settings.Root, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var key = Path.GetRelativePath(settings.Root, file).Replace(Path.DirectorySeparatorChar, '/');
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    keys.Add(key);
            }
            return Task.FromResult((IReadOnlyList<string>)keys);
        });

    private CancellationToken CancellationToken()
    {
        var ct = accessor.AxisMediator?.CancellationToken ?? System.Threading.CancellationToken.None;
        ct.ThrowIfCancellationRequested();
        return ct;
    }

    private string PathFor(string key) => Path.Combine(settings.Root, key.Replace('/', Path.DirectorySeparatorChar));
}
