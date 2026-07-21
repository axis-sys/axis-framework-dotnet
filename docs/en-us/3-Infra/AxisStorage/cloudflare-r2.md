# Cloudflare R2 adapter · `AxisStorage.CloudflareR2`

> A bundled implementation of `IAxisStorage` — an `AmazonS3Client` configured for Cloudflare R2's S3-compatible endpoint, fronted by a typed `CloudflareR2Settings` object. It also implements `IAxisStorageContainer`, `IAxisStorageLister`, and `IAxisStorageUrlResolver`. The adapter type is `internal sealed` — consume it through DI or the runtime-destination factory `ICloudflareR2StorageFactory`, never by `new`.

```csharp
services.AddAxisCloudflareR2Storage(new CloudflareR2Settings
{
    AccountId  = "abc123",
    AccessKey  = "...",
    SecretKey  = "...",
    BucketName = "uploads",
    PublicUrl  = "https://cdn.example.com",   // optional
});
```

---

## When to use

You picked Cloudflare R2 (or another S3-compatible store reachable via the AWS SDK, like MinIO in tests). The adapter does not assume R2-specific features — anything that responds to the S3 API works.

## When *not* to use

| You want to… | Use instead |
|---|---|
| target Azure Blob Storage | the in-box [`AxisStorage.AzureBlob`](azure-blob.md) |
| target plain AWS S3 with regional endpoints | a custom adapter using `RegionEndpoint` directly |
| store to local disk / a mounted NAS | the in-box [`AxisStorage.FileSystem`](filesystem.md) |

---

## Settings — `CloudflareR2Settings`

| Property | Type | Description |
|---|---|---|
| `AccountId` | `string` (required) | the R2 account id — used to build the service URL `https://{AccountId}.r2.cloudflarestorage.com` |
| `AccessKey` | `string` (required) | the R2 access key id |
| `SecretKey` | `string` (required) | the R2 secret access key |
| `BucketName` | `string` (required) | the bucket every operation uses |
| `PublicUrl` | `string?` | optional public URL prefix (a CDN in front of the bucket, or the bucket's `r2.dev` domain). When set, `GetServableUrlAsync` returns `{PublicUrl}/{key}` as a **raw public URL**; when null, it falls back to a **presigned** URL — see [Servable URLs and `PublicUrl`](#servable-urls-and-publicurl) |
| `ServiceUrl` | `string` (computed) | `https://{AccountId}.r2.cloudflarestorage.com` — internal, used to configure the S3 client |

---

## What gets registered

Reading `DependencyInjection.AddAxisCloudflareR2Storage` directly:

```csharp
var s3Client = new AmazonS3Client(
    settings.AccessKey,
    settings.SecretKey,
    new AmazonS3Config
    {
        ServiceURL           = settings.ServiceUrl,            // R2 endpoint
        AuthenticationRegion = RegionEndpoint.USEast1.SystemName,
        ForcePathStyle       = true,                           // R2 requires path-style
    });

services.AddSingleton(settings);
services.AddSingleton<IAmazonS3>(s3Client);
services.AddSingleton<CloudflareR2StorageAdapter>();
services.AddSingleton<IAxisStorage>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
services.AddSingleton<IAxisStorageContainer>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
services.AddSingleton<IAxisStorageLister>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
services.AddSingleton<IAxisStorageUrlResolver>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
```

- `CloudflareR2Settings` is registered as a singleton.
- `IAmazonS3` is constructed once and registered as a singleton — connection pooling is handled inside the SDK.
- One `CloudflareR2StorageAdapter` instance is exposed under all four interfaces it implements — `IAxisStorage`, `IAxisStorageContainer`, `IAxisStorageLister`, `IAxisStorageUrlResolver` — same registration shape as `AxisStorage.AzureBlob`.

---

## Runtime destination — `ICloudflareR2StorageFactory`

For a destination chosen at runtime (a multi-tenant app with a bucket per tenant), register the factory:

```csharp
builder.Services.AddAxisCloudflareR2StorageFactory();

// later, in a handler:
var storage = factory.Create(new CloudflareR2Settings
{
    AccountId  = tenant.AccountId,
    AccessKey  = tenant.AccessKey,
    SecretKey  = tenant.SecretKey,
    BucketName = tenant.Bucket,
    PublicUrl  = tenant.PublicUrl,
});
```

- `AddAxisCloudflareR2StorageFactory()` registers `ICloudflareR2StorageFactory` as a **Singleton**.
- `Create(CloudflareR2Settings destination)` returns an `IAxisStorage`, caching one adapter per destination (keyed by `{AccountId}/{BucketName}`).
- Unlike the Azure factory, `Create` takes the **whole** `CloudflareR2Settings` per call, not just an account/container — R2 credentials (`AccessKey`/`SecretKey`) are per-bucket, so there is no single process-wide credential to reuse.
- The returned instance also implements the optional capabilities — reach them by cast (`... as IAxisStorageUrlResolver`).

---

## How each method maps to `IAmazonS3`

| `IAxisStorage` / `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver` | AWS SDK call | Notes |
|---|---|---|
| `UploadAsync` | `PutObjectAsync(PutObjectRequest)` | streams the body, sets `Content-Type` |
| `DownloadAsync` | `GetObjectAsync(GetObjectRequest)` | returns `ResponseStream` directly |
| `DeleteAsync` | `DeleteObjectAsync(DeleteObjectRequest)` | idempotent |
| `ExistsAsync(key)` | `GetObjectMetadataAsync(GetObjectMetadataRequest)` | catches `AmazonS3Exception` with `StatusCode == NotFound` and returns `Ok(false)` |
| `GetPresignedUrlAsync` | `GetPreSignedURLAsync(GetPreSignedUrlRequest)` | `Verb = GET`, `Expires = UtcNow + expiration` |
| `IAxisStorageContainer.ExistsAsync()` | `GetBucketLocationAsync` | catches `AmazonS3Exception` with `StatusCode == NotFound` and returns `Ok(false)` |
| `IAxisStorageContainer.EnsureExistsAsync()` | `PutBucketAsync` | tolerates `ErrorCode == "BucketAlreadyOwnedByYou"` — idempotent |
| `IAxisStorageContainer.IsPubliclyAccessibleAsync()` | `GetBucketAclAsync` | `true` when the `AllUsers` grantee has `READ`; Cloudflare R2 manages public access outside the legacy ACL API in some configurations — verify against your bucket before relying on this in production |
| `IAxisStorageLister.ListAsync(prefix)` | `ListObjectsV2Async` (paginated via `ContinuationToken`) | returns every key under `prefix` |
| `IAxisStorageUrlResolver.GetServableUrlAsync` | none (when `PublicUrl` set) / `GetPreSignedURLAsync` (otherwise) | returns an `AxisStorageUrl` — see below |

Every method wraps the SDK call in `AxisResult.TryAsync`, so any thrown exception becomes a typed `AxisResult` failure.

---

## Servable URLs and `PublicUrl`

`GetServableUrlAsync(key, expiration)` implements [`IAxisStorageUrlResolver`](iaxisstorage.md#optional-capabilities--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver) and is where `CloudflareR2Settings.PublicUrl` finally earns its keep — it decides public-vs-signed from that one field:

- **`PublicUrl` set** → `{PublicUrl.TrimEnd('/')}/{key}`, a raw public URL (`IsPublic: true`, `ExpiresAt: null`). No S3 call — R2 serves the object over the CDN/`r2.dev` domain directly.
- **`PublicUrl` null/blank** → a presigned URL (`IsPublic: false`, `ExpiresAt = now + expiration`), the same signing path as `GetPresignedUrlAsync`.

Unlike the Azure adapter, R2 makes the decision from configuration (`PublicUrl`), not from a live public-access probe — so there is no cache and no TTL here.

---

## Real-world example — bind from configuration

```csharp
// appsettings.json
// {
//   "Storage": {
//     "AccountId":  "abc123",
//     "AccessKey":  "...",
//     "SecretKey":  "...",
//     "BucketName": "uploads"
//   }
// }

var settings = builder.Configuration.GetSection("Storage").Get<CloudflareR2Settings>()!;
builder.Services.AddAxisCloudflareR2Storage(settings);
```

**Why it pays off:** the secrets live in configuration (`appsettings`, environment, key vault). The adapter binds them once at startup, and your handlers see only `IAxisStorage`.

---

## See also

- [The `IAxisStorage` contract](iaxisstorage.md) — the five operations, plus the optional `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver`
- [Upload / Download](upload-download.md) — the streaming pattern
- [Presigned URLs](presigned-urls.md) — the URL minting
- [Azure Blob adapter](azure-blob.md) — the other cloud implementation
- [FileSystem adapter](filesystem.md) — the local-disk / NAS implementation
- [Custom adapter](custom-adapter.md) — wrap another backend

---

↩ [Back to AxisStorage docs](README.md)
