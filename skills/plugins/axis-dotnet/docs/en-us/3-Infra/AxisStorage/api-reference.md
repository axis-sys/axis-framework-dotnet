# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## The contract — `IAxisStorage`

| Method | Signature | Description |
|---|---|---|
| `UploadAsync` | `Task<AxisResult> UploadAsync(string key, Stream content, string contentType)` | streaming upload; overwrites silently |
| `DownloadAsync` | `Task<AxisResult<Stream>> DownloadAsync(string key)` | streaming download; **caller disposes** the stream |
| `DeleteAsync` | `Task<AxisResult> DeleteAsync(string key)` | idempotent removal |
| `ExistsAsync` | `Task<AxisResult<bool>> ExistsAsync(string key)` | non-throwing existence check |
| `GetPresignedUrlAsync` | `Task<AxisResult<string>> GetPresignedUrlAsync(string key, TimeSpan expiration)` | minted GET URL, valid for `now + expiration` |

→ [The `IAxisStorage` contract](iaxisstorage.md) · [Upload / Download](upload-download.md) · [Presigned URLs](presigned-urls.md)

---

## The optional interfaces — `IAxisStorageContainer`, `IAxisStorageLister`, `IAxisStorageUrlResolver`

| Method | Signature | Description |
|---|---|---|
| `ExistsAsync` | `Task<AxisResult<bool>> ExistsAsync()` | container/bucket-level existence check |
| `EnsureExistsAsync` | `Task<AxisResult> EnsureExistsAsync()` | idempotent container/bucket provisioning |
| `IsPubliclyAccessibleAsync` | `Task<AxisResult<bool>> IsPubliclyAccessibleAsync()` | *reports* whether the container/bucket serves objects without a signed URL |
| `ListAsync` | `Task<AxisResult<IReadOnlyList<string>>> ListAsync(string prefix)` | every key under `prefix` |
| `GetServableUrlAsync` | `Task<AxisResult<AxisStorageUrl>> GetServableUrlAsync(string key, TimeSpan expiration)` | *decides and emits* the URL a client should use — raw when the container is public, signed when it is private |

`AxisStorageUrl` — `public sealed record AxisStorageUrl(string Url, bool IsPublic, DateTimeOffset? ExpiresAt)`: the resolved `Url`, whether it is a raw public URL (`IsPublic`), and when a signed URL expires (`ExpiresAt`, `null` when public).

→ [The `IAxisStorage` contract — optional capabilities](iaxisstorage.md#optional-capabilities--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver)

---

## Cloudflare R2 adapter — `AxisStorage.CloudflareR2`

| Member | Description |
|---|---|
| `CloudflareR2Settings` | typed configuration: `AccountId`, `AccessKey`, `SecretKey`, `BucketName`, `PublicUrl?` (drives public-vs-signed in `GetServableUrlAsync`) |
| `CloudflareR2StorageAdapter` | `internal sealed`; implements `IAxisStorage` + `IAxisStorageContainer` + `IAxisStorageLister` + `IAxisStorageUrlResolver`; not instantiable by the consumer |
| `ICloudflareR2StorageFactory` | `IAxisStorage Create(CloudflareR2Settings destination)` — one adapter per destination for runtime-chosen buckets (full settings per call, since R2 credentials are per-bucket) |
| `services.AddAxisCloudflareR2Storage(settings)` | DI extension; registers settings + `IAmazonS3` + the adapter under `IAxisStorage`/`IAxisStorageContainer`/`IAxisStorageLister`/`IAxisStorageUrlResolver` (singletons, same instance) |
| `services.AddAxisCloudflareR2StorageFactory()` | DI extension; registers `ICloudflareR2StorageFactory` (Singleton) |

→ [Cloudflare R2 adapter](cloudflare-r2.md)

---

## Azure Blob adapter — `AxisStorage.AzureBlob`

| Member | Description |
|---|---|
| `AzureBlobCredentialSettings` | typed identity configuration: `TenantId?`, `ClientId?`, `ClientSecret?`, `ManagedIdentityClientId?` |
| `AzureBlobSettings` | typed destination configuration: `AccountUrl`, `Container` |
| `AzureBlobStorageOptions` | `public sealed class`; `TimeSpan PublicAccessCacheTtl` (default 15 min) — TTL for the cached public-access probe on the `GetServableUrlAsync` path |
| `AzureBlobCredential.Create(AzureBlobCredentialSettings)` | static resolver: Service Principal → scoped `DefaultAzureCredential` → plain `DefaultAzureCredential` |
| `AzureBlobStorageAdapter` | `internal sealed`; implements `IAxisStorage` + `IAxisStorageContainer` + `IAxisStorageLister` + `IAxisStorageUrlResolver`; not instantiable by the consumer |
| `IAzureBlobStorageFactory` | `IAxisStorage Create(AzureBlobSettings destination)` — one adapter per destination (keyed by `{AccountUrl}/{Container}`), reusing one process-wide credential |
| `services.AddAxisAzureBlobStorage(credentialSettings, storageSettings, configure?)` | DI extension; registers settings + `AzureBlobStorageOptions` + `BlobServiceClient` + the adapter under `IAxisStorage`/`IAxisStorageContainer`/`IAxisStorageLister`/`IAxisStorageUrlResolver` (singletons, same instance). `configure` is an optional `Action<AzureBlobStorageOptions>` |
| `services.AddAxisAzureBlobStorageFactory(credentialSettings, configure?)` | DI extension; registers `IAzureBlobStorageFactory` (Singleton), one credential per process |

→ [Azure Blob adapter](azure-blob.md)

---

## FileSystem adapter — `AxisStorage.FileSystem`

| Member | Description |
|---|---|
| `FileSystemStorageSettings` | `public sealed class`; `required string Root` — the base directory (local disk, mounted NAS, or mounted file server) |
| `FileSystemStorageAdapter` | `internal sealed`; implements `IAxisStorage` + `IAxisStorageContainer` + `IAxisStorageLister` — **not** `IAxisStorageUrlResolver`. `GetPresignedUrlAsync` returns a `file://` URI; `IsPubliclyAccessibleAsync()` returns `Ok(true)`; `EnsureExistsAsync()` creates the directory |
| `IFileSystemStorageFactory` | `IAxisStorage Create(FileSystemStorageSettings destination)` — one adapter per `Root` |
| `services.AddAxisFileSystemStorage(settings)` | DI extension; registers settings + the adapter under `IAxisStorage`/`IAxisStorageContainer`/`IAxisStorageLister` (singletons, same instance) |
| `services.AddAxisFileSystemStorageFactory()` | DI extension; registers `IFileSystemStorageFactory` (Singleton) |

→ [FileSystem adapter](filesystem.md)

---

## Behaviour contract (for adapters)

| Operation | Object state | Returned `AxisResult` | Object state after |
|---|---|---|---|
| `UploadAsync` | any | `Ok()` | written/overwritten |
| `DownloadAsync` | exists | `Ok(stream)` | unchanged |
| `DownloadAsync` | missing | `Error(...)` | unchanged |
| `DeleteAsync` | any | `Ok()` | removed (if it existed) |
| `ExistsAsync` | exists | `Ok(true)` | unchanged |
| `ExistsAsync` | missing | `Ok(false)` | unchanged |
| `GetPresignedUrlAsync` | n/a (no existence check) | `Ok(url)` | unchanged |
| `GetServableUrlAsync` (optional) | n/a (no existence check) | `Ok(AxisStorageUrl)` | unchanged |
| any | n/a | adapter threw | `Error(InternalServerError(...))` |
| any | n/a | cancelled | *(throws `OperationCanceledException` — no `AxisResult` returned)* |

> Cancellation is the deliberate exception to "storage never throws": a cancelled ambient token propagates as a real `OperationCanceledException`, not as a failed `AxisResult`.

→ [Custom adapter](custom-adapter.md)

---

## See also

- [Getting started](getting-started.md) — install, register, upload
- [Why AxisStorage?](why-axisstorage.md) — the case for the abstraction
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisStorage docs](README.md)
