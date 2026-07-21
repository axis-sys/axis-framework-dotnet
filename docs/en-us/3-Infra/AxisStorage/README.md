# AxisStorage — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/3-Infra/AxisStorage/README.md)

**An object-storage port for C#** — `IAxisStorage` with five async operations (`Upload`, `Download`, `Delete`, `Exists`, `GetPresignedUrl`), every one returning `AxisResult`. Three optional interfaces — `IAxisStorageContainer`, `IAxisStorageLister`, `IAxisStorageUrlResolver` — cover container administration, prefix listing, and servable-URL resolution for adapters that support them. Three bundled adapters: `AxisStorage.CloudflareR2` wires the Amazon S3 SDK against Cloudflare R2 (the same contract works for plain S3, MinIO and any other S3-compatible bucket), `AxisStorage.AzureBlob` wires `Azure.Storage.Blobs` against Azure Blob Storage — both implement every optional interface — and `AxisStorage.FileSystem` stores to local disk or a mounted NAS (all optional interfaces except `IAxisStorageUrlResolver`).

```csharp
public Task<AxisResult<UploadAvatarResponse>> HandleAsync(UploadAvatarCommand cmd)
{
    var key = $"avatars/{cmd.PersonId}.png";

    return storage.UploadAsync(key, cmd.Content, "image/png")
        .ThenAsync(() => storage.GetPresignedUrlAsync(key, TimeSpan.FromHours(1)))
        .MapAsync(url => new UploadAvatarResponse { Url = url });
}
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### The interface in 60 seconds

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

Five methods. Keys are strings; vendor concepts (bucket, region, endpoint) live in the **adapter**'s configuration. Cancellation flows in from `IAxisMediatorAccessor`. → **[The `IAxisStorage` contract](iaxisstorage.md)**

### Why a presigned URL?

`GetPresignedUrlAsync` returns a time-limited URL the **client** can use to fetch the object directly from the bucket — no proxying through your API. Cheaper, faster, and one less stream for your server to hold open. → **[Presigned URLs](presigned-urls.md)**

### Bundled adapters — Cloudflare R2, Azure Blob and FileSystem

`AxisStorage.CloudflareR2` is an Amazon S3 SDK adapter wired against R2's endpoint, with a typed `CloudflareR2Settings` (account id, access keys, bucket, optional public URL).

```csharp
services.AddAxisCloudflareR2Storage(new CloudflareR2Settings
{
    AccountId  = "...",
    AccessKey  = "...",
    SecretKey  = "...",
    BucketName = "uploads",
});
```

`AxisStorage.AzureBlob` is an `Azure.Storage.Blobs` adapter, with a typed `AzureBlobCredentialSettings` (Azure AD identity) and `AzureBlobSettings` (account + container).

```csharp
services.AddAxisAzureBlobStorage(
    new AzureBlobCredentialSettings { /* empty ⇒ ambient DefaultAzureCredential */ },
    new AzureBlobSettings { AccountUrl = "https://myaccount.blob.core.windows.net", Container = "uploads" });
```

`AxisStorage.FileSystem` stores to a directory on disk (local disk, mounted NAS, mounted file server), with a typed `FileSystemStorageSettings` (`Root`).

```csharp
services.AddAxisFileSystemStorage(new FileSystemStorageSettings { Root = "/var/lib/myapp/blobs" });
```

→ **[Cloudflare R2 adapter](cloudflare-r2.md)** · **[Azure Blob adapter](azure-blob.md)** · **[FileSystem adapter](filesystem.md)**

For a destination chosen at runtime (multi-tenant), each provider ships a factory — `IAzureBlobStorageFactory`, `ICloudflareR2StorageFactory`, `IFileSystemStorageFactory` — registered by `AddAxis…StorageFactory(...)`.

### Installation

```
dotnet add package AxisStorage                  # the abstraction
dotnet add package AxisStorage.CloudflareR2     # the Cloudflare R2 adapter (uses AWSSDK.S3)
dotnet add package AxisStorage.AzureBlob        # the Azure Blob adapter (uses Azure.Storage.Blobs)
dotnet add package AxisStorage.FileSystem       # the disk / NAS adapter (no extra dependency)
```

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Contract · `IAxisStorage`** | the five operations, plus the optional `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver` | [iaxisstorage.md](iaxisstorage.md) |
| **Upload / Download · `UploadAsync`, `DownloadAsync`** ⭐ | move bytes in and out of the bucket | [upload-download.md](upload-download.md) |
| **Presigned URLs · `GetPresignedUrlAsync`** | hand the client a time-limited URL | [presigned-urls.md](presigned-urls.md) |
| **Cloudflare R2 · `AxisStorage.CloudflareR2`** | a bundled adapter | [cloudflare-r2.md](cloudflare-r2.md) |
| **Azure Blob · `AxisStorage.AzureBlob`** | another bundled adapter | [azure-blob.md](azure-blob.md) |
| **FileSystem · `AxisStorage.FileSystem`** | the disk / NAS adapter | [filesystem.md](filesystem.md) |
| **Custom adapter** | write another one (plain S3, GCS) | [custom-adapter.md](custom-adapter.md) |
| **Why?** | the case against direct `IAmazonS3` | [why-axisstorage.md](why-axisstorage.md) |
| **Reference** | every method at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [The `IAxisStorage` contract](iaxisstorage.md) · [Why AxisStorage?](why-axisstorage.md)

**Fundamentals:** [Upload / Download](upload-download.md) · [Presigned URLs](presigned-urls.md) · [Cloudflare R2 adapter](cloudflare-r2.md) · [Azure Blob adapter](azure-blob.md) · [FileSystem adapter](filesystem.md)

**Reference & extras:** [Custom adapter](custom-adapter.md) · [API reference](api-reference.md)

---

## Design principles

1. **Vendor-neutral keys, vendor-specific config.** Application code talks strings; bucket / region / endpoint live in the adapter's settings.
2. **Streams in, streams out.** No buffer-the-whole-blob abstraction — the contract passes `Stream`, so large uploads and downloads work without OOM.
3. **`Exists` is a typed boolean, not an exception.** `Ok(false)` for a missing key; `IsFailure` only when the call itself blew up.
4. **Presigned URLs are first-class.** Direct-to-client downloads save your egress; they are part of the contract, not a vendor escape hatch.
5. **The vendor SDK stays at the edge.** The bundled adapters wrap `IAmazonS3` and `BlobServiceClient` respectively, so application code never touches `PutObjectRequest` or `BlobClient` directly.
6. **Optional capabilities are separate interfaces, not optional methods.** Container administration (`IAxisStorageContainer`), prefix listing (`IAxisStorageLister`) and servable-URL resolution (`IAxisStorageUrlResolver`) live outside `IAxisStorage` so a provider that can't support one isn't forced to fake it — `AxisStorage.FileSystem`, for instance, implements the first two but not the third.

---

## License

Apache 2.0
