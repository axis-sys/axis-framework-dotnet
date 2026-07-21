# Contract · `IAxisStorage`

> A vendor-neutral object-storage port. Five async methods, every result an `AxisResult`. Keys are strings; the bucket, region and endpoint are the adapter's business.

```csharp
public interface IAxisStorage
{
    Task<AxisResult>         UploadAsync(string key, Stream content, string contentType);
    Task<AxisResult<Stream>> DownloadAsync(string key);
    Task<AxisResult>         DeleteAsync(string key);
    Task<AxisResult<bool>>   ExistsAsync(string key);
    Task<AxisResult<string>> GetPresignedUrlAsync(string key, TimeSpan expiration);
}
```

---

## When to use

Anywhere your application has to persist or read **binary content** that does not belong in a relational database: avatar images, document attachments, exports, logs you want to keep cold. Keys mirror a file path (`avatars/{id}.png`), the rest is up to your naming convention.

## When *not* to use

| You want to… | Use instead |
|---|---|
| store **structured** records | a database via [`AxisRepository`](../AxisRepository/README.md) |
| send a **transactional email attachment** | resolve a presigned URL and pass it to [`AxisEmail`](../AxisEmail/README.md) |
| cache something | [`AxisCache`](../AxisCache/README.md) |
| host a CDN | a real CDN — `AxisStorage` is the origin |

---

## The five operations

| Method | Success means | `IsFailure` when |
|---|---|---|
| `UploadAsync(key, stream, contentType)` | the stream was written under `key`, overwriting any existing object | the adapter threw |
| `DownloadAsync(key)` | the stream is ready to read | the object does not exist or the adapter threw |
| `DeleteAsync(key)` | the key is gone (deleting a missing key is still success) | the adapter threw |
| `ExistsAsync(key)` | the call completed; `Value` tells you whether it was there | the adapter threw on a non-404 error |
| `GetPresignedUrlAsync(key, expiration)` | a signed URL good until `now + expiration` was minted | the adapter threw |

> Implicit cancellation: every method in the bundled `CloudflareR2StorageAdapter` pulls the `CancellationToken` from `accessor.AxisMediator!.CancellationToken` and threads it into the AWS S3 SDK call. A cancelled ambient token is the one deliberate exception to "storage never throws": it surfaces as a real `OperationCanceledException`, never as an `IsFailure` `AxisResult`.

---

## Optional capabilities — `IAxisStorageContainer`, `IAxisStorageLister`, `IAxisStorageUrlResolver`

`IAxisStorage` stays at five methods on purpose — every provider must implement all of them. Container-level administration, prefix listing, and servable-URL resolution are **not** universal enough to force on every adapter (they don't always map cleanly to every object store), so they live in three separate, optional interfaces in the same `AxisStorage` package. The two cloud adapters (`AxisStorage.CloudflareR2`, `AxisStorage.AzureBlob`) implement all three; `AxisStorage.FileSystem` implements the first two but **not** `IAxisStorageUrlResolver` (how a file on disk/NAS gets served over HTTP is the business of whatever fronts it, not of the storage port); a future adapter is free to implement only `IAxisStorage`.

```csharp
public interface IAxisStorageContainer
{
    Task<AxisResult<bool>> ExistsAsync();
    Task<AxisResult>       EnsureExistsAsync();
    Task<AxisResult<bool>> IsPubliclyAccessibleAsync();
}

public interface IAxisStorageLister
{
    Task<AxisResult<IReadOnlyList<string>>> ListAsync(string prefix);
}

public interface IAxisStorageUrlResolver
{
    Task<AxisResult<AxisStorageUrl>> GetServableUrlAsync(string key, TimeSpan expiration);
}

public sealed record AxisStorageUrl(string Url, bool IsPublic, DateTimeOffset? ExpiresAt);
```

| Method | Success means | Use it for |
|---|---|---|
| `IAxisStorageContainer.ExistsAsync()` | the bucket/container itself exists | pre-flight check before an operation that assumes the container is there |
| `IAxisStorageContainer.EnsureExistsAsync()` | the bucket/container exists after the call (created if missing) | explicit provisioning — call it at tenant/environment setup time, not implicitly on first upload |
| `IAxisStorageContainer.IsPubliclyAccessibleAsync()` | `Value` reflects whether the bucket/container serves objects without a signed URL | deciding whether to hand back a raw public URL or a `GetPresignedUrlAsync` result |
| `IAxisStorageLister.ListAsync(prefix)` | `Value` holds every key under `prefix` | import/reconciliation jobs that need to enumerate what is already stored, not just fetch a known key |
| `IAxisStorageUrlResolver.GetServableUrlAsync(key, expiration)` | `Value` is the finished `AxisStorageUrl` a client should use — the raw public URL when the container serves objects publicly, otherwise a signed URL valid for `expiration` | handing a browser one URL without branching on public-vs-private yourself |

### `IsPubliclyAccessibleAsync` *reports*; `GetServableUrlAsync` *decides and emits*

The two look related but answer different questions. `IAxisStorageContainer.IsPubliclyAccessibleAsync` **reports a fact** — whether the container currently serves objects without a signature — and leaves the branching to you. `IAxisStorageUrlResolver.GetServableUrlAsync` **decides and emits**: it consults that same fact and returns the finished URL — a raw URL (`IsPublic: true`, `ExpiresAt: null`) when the container is public, or a signed URL (`IsPublic: false`, `ExpiresAt` set to `now + expiration`) when it is not. The returned `AxisStorageUrl` carries all three so the caller need not re-derive them.

Reach for the resolver when you just want the URL to hand back to a client; reach for the reporter when you need the raw fact for some other decision. (The Azure adapter caches the "is public" answer for a short, configurable TTL on the resolver path — see [Azure Blob adapter](azure-blob.md).)

Since these are separate interfaces, inject exactly the one your handler needs — most handlers only ever need `IAxisStorage`; container / listing / URL-resolution capability is for administrative, import-style, or download-serving code paths. `IAxisStorageUrlResolver` is either injected directly (both cloud adapters register it in DI) or reached by casting an `IAxisStorage` you already hold — `storage as IAxisStorageUrlResolver` — which is how you obtain it from a [provider factory](azure-blob.md#runtime-destination--iazureblobstoragefactory) that hands back a bare `IAxisStorage`.

```csharp
public class ProvisionTenantStorageHandler(IAxisStorageContainer container) : IAxisCommandHandler<...>
{
    public Task<AxisResult> HandleAsync(ProvisionTenantStorageCommand cmd)
        => container.EnsureExistsAsync();
}
```

---

## Real-world examples

### 1. Upload, then return a download URL

```csharp
public Task<AxisResult<UploadAvatarResponse>> HandleAsync(UploadAvatarCommand cmd)
{
    var key = $"avatars/{cmd.PersonId}.png";

    return storage.UploadAsync(key, cmd.Content, "image/png")
        .ThenAsync(() => storage.GetPresignedUrlAsync(key, TimeSpan.FromHours(1)))
        .MapAsync(url => new UploadAvatarResponse { Url = url });
}
```

**Why it pays off:** the railway carries one failure mode through both steps. If the upload fails, the URL is never minted; if the URL minting fails, the upload is already committed and an `AxisError` carries the reason.

### 2. Idempotent delete

```csharp
public Task<AxisResult> RemoveAvatarAsync(AxisEntityId personId)
    => storage.DeleteAsync($"avatars/{personId}.png");
```

**Why it pays off:** `DeleteAsync` succeeds whether the key existed or not — callers do not need to `ExistsAsync` first. Idempotent at the contract level.

### 3. Exists-before-download for big files

```csharp
public async Task<AxisResult<Stream>> GetExportAsync(AxisEntityId exportId)
{
    var key = $"exports/{exportId}.csv";

    var exists = await storage.ExistsAsync(key);
    if (exists.IsFailure || !exists.Value)
        return AxisError.NotFound("EXPORT_NOT_FOUND");

    return await storage.DownloadAsync(key);
}
```

**Why it pays off:** the typed `Ok(false)` saves a 404-shaped exception from the SDK on a known-missing path, and the handler reads cleanly without an exception-driven branch.

---

## See also

- [Upload / Download](upload-download.md) — the streaming workhorses
- [Presigned URLs](presigned-urls.md) — direct-to-client downloads
- [Cloudflare R2 adapter](cloudflare-r2.md) — the in-box implementation
- [Azure Blob adapter](azure-blob.md) — the other in-box implementation
- [Custom adapter](custom-adapter.md) — implement `IAxisStorage` for your storage
- [API reference](api-reference.md) — every method, in one place

---

↩ [Back to AxisStorage docs](README.md)
