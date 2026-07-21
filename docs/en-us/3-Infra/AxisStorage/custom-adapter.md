# Custom adapter · write your own `IAxisStorage`

> Add a third provider next to `AxisStorage.CloudflareR2` and `AxisStorage.AzureBlob` — plain AWS S3 (not via R2), Google Cloud Storage, a local-disk implementation for tests, or a hybrid that reads from one backend and writes to two. Implement five methods, register your class for `IAxisStorage`.

```csharp
public class GoogleCloudStorageAdapter(StorageClient client, string bucket, IAxisMediatorAccessor accessor) : IAxisStorage
{
    private CancellationToken Ct => accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;

    public Task<AxisResult> UploadAsync(string key, Stream content, string contentType)
        => AxisResult.TryAsync(async () =>
        {
            Ct.ThrowIfCancellationRequested();
            await client.UploadObjectAsync(bucket, key, contentType, content, cancellationToken: Ct);
        });

    // … DownloadAsync, DeleteAsync, ExistsAsync, GetPresignedUrlAsync
}
```

`AxisStorage.AzureBlob` (see [its own page](azure-blob.md)) is a full, non-illustrative example of this pattern worth reading end to end before writing a third provider.

---

## When to use

- Plain AWS S3 (not via R2), Google Cloud Storage, or any other object store.
- A **local-disk** adapter for unit tests.
- A **read-from-one, write-to-many** mirror adapter for migration.

## When *not* to use

| You want to… | Use instead |
|---|---|
| target R2 / S3-compatible | the in-box [`AxisStorage.CloudflareR2`](cloudflare-r2.md) |
| target Azure Blob Storage | the in-box [`AxisStorage.AzureBlob`](azure-blob.md) |
| add bucket-level features (versioning, tagging) | extend the contract via a new interface, alongside `IAxisStorageContainer`/`IAxisStorageLister`/`IAxisStorageUrlResolver`, in the same `AxisStorage` package, and require the *adapter* to implement it (→ [optional capabilities](iaxisstorage.md#optional-capabilities--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver)) |
| resolve a **different** account/container per call (multi-tenant storage) | inject the provider's factory — `IAzureBlobStorageFactory` / `ICloudflareR2StorageFactory` / `IFileSystemStorageFactory` — and call `Create(destination)`. The in-box adapters are `internal` by design and are never instantiated by the consumer (→ [Azure Blob · Runtime destination](azure-blob.md#runtime-destination--iazureblobstoragefactory)) |

---

## The contract you must honour

| Behaviour | Required | Rationale |
|---|---|---|
| Every method returns `AxisResult`, never throws cooperatively | yes | the railway depends on it |
| `UploadAsync` overwrites silently | yes | matches S3 semantics; if you need if-not-exists, expose a new method |
| `DownloadAsync` returns a `Stream` the caller disposes | yes | streaming, not buffering |
| `ExistsAsync` returns `Ok(false)` on a missing key, **not** a failure | yes | so callers can branch without exception flow |
| `DeleteAsync` is idempotent | yes | match S3 semantics |
| Honour cancellation from `IAxisMediatorAccessor.AxisMediator?.CancellationToken` | recommended | matches the in-box adapter |
| Log via `AxisLogger` | recommended | enrichers attach correlation / tenant |

---

## Real-world example — local-disk adapter for tests

> For **real** disk or mounted-NAS storage, don't hand-roll this — use the in-box [`AxisStorage.FileSystem`](filesystem.md) provider. The example below only illustrates the shape of the contract.

```csharp
public class LocalDiskStorageAdapter(string root, IAxisMediatorAccessor accessor) : IAxisStorage
{
    private CancellationToken Ct => accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;

    private string Path(string key) => System.IO.Path.Combine(root, key.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public Task<AxisResult> UploadAsync(string key, Stream content, string contentType)
        => AxisResult.TryAsync(async () =>
        {
            var path = Path(key);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            await using var fs = File.Create(path);
            await content.CopyToAsync(fs, Ct);
        });

    public Task<AxisResult<Stream>> DownloadAsync(string key)
        => AxisResult.TryAsync(() =>
        {
            Ct.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(File.OpenRead(Path(key)));
        });

    public Task<AxisResult> DeleteAsync(string key)
        => AxisResult.TryAsync(() =>
        {
            var path = Path(key);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        });

    public Task<AxisResult<bool>> ExistsAsync(string key)
        => AxisResult.TryAsync(() => Task.FromResult(File.Exists(Path(key))));

    public Task<AxisResult<string>> GetPresignedUrlAsync(string key, TimeSpan expiration)
        => AxisResult.TryAsync(() => Task.FromResult($"file://{Path(key)}"));
}
```

**Why it pays off:** integration tests run with **no network**, the same `IAxisStorage` your production code consumes — and CI never needs to mock the AWS SDK by hand.

---

## See also

- [The `IAxisStorage` contract](iaxisstorage.md) — the surface you must satisfy
- [Cloudflare R2 adapter](cloudflare-r2.md) — an in-box reference
- [Azure Blob adapter](azure-blob.md) — the other in-box reference, and the one to copy for a real (non-illustrative) third provider
- [FileSystem adapter](filesystem.md) — the in-box disk/NAS provider (before writing your own local-disk adapter)
- [Upload / Download](upload-download.md) — streaming semantics
- [Presigned URLs](presigned-urls.md) — URL minting

---

↩ [Back to AxisStorage docs](README.md)
