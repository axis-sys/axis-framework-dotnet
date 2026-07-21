# Getting started · installation and usage

> Install the abstraction and the Cloudflare R2 adapter, register them in DI, and upload your first file in five minutes.

---

## Installation

```
dotnet add package AxisStorage                  # the abstraction
dotnet add package AxisStorage.CloudflareR2     # the Cloudflare R2 adapter
```

`AxisStorage` depends only on `AxisResult`. `AxisStorage.CloudflareR2` adds `AWSSDK.S3` (R2 speaks the S3 API).

---

## Configuring R2

R2 needs four secrets plus the bucket name. Put them in `appsettings.json`:

```json
{
  "Storage": {
    "AccountId":  "abc123…",
    "AccessKey":  "…",
    "SecretKey":  "…",
    "BucketName": "uploads",
    "PublicUrl":  "https://cdn.example.com"
  }
}
```

---

## Registering the adapter

```csharp
using AxisStorage.CloudflareR2;

var settings = builder.Configuration.GetSection("Storage").Get<CloudflareR2Settings>()!;
builder.Services.AddAxisCloudflareR2Storage(settings);
```

`AddAxisCloudflareR2Storage` constructs an `AmazonS3Client` against R2's endpoint (`https://{AccountId}.r2.cloudflarestorage.com`), registers the settings as a singleton, and binds `IAxisStorage → CloudflareR2StorageAdapter`.

---

## Uploading

```csharp
public async Task<AxisResult<UploadAvatarResponse>> HandleAsync(UploadAvatarCommand cmd)
{
    var key = $"avatars/{cmd.PersonId}.png";

    return await storage.UploadAsync(key, cmd.Content, "image/png")
        .ThenAsync(() => storage.GetPresignedUrlAsync(key, TimeSpan.FromHours(1)))
        .MapAsync(url => new UploadAvatarResponse { Url = url });
}
```

---

## Downloading

```csharp
public Task<AxisResult<Stream>> GetAvatarAsync(AxisEntityId personId)
    => storage.DownloadAsync($"avatars/{personId}.png");
```

---

## Existence and deletion

```csharp
var exists = await storage.ExistsAsync($"avatars/{personId}.png");

if (exists.IsSuccess && exists.Value)
    await storage.DeleteAsync($"avatars/{personId}.png");
```

**Why it pays off:** the same five-method surface works for R2 today, plain S3 tomorrow, MinIO in tests — your handlers do not change.

---

## See also

- [The `IAxisStorage` contract](iaxisstorage.md) — every method, semantics, failure modes
- [Upload / Download](upload-download.md) — the streaming workhorses
- [Presigned URLs](presigned-urls.md) — direct-to-client downloads
- [Cloudflare R2 adapter](cloudflare-r2.md) — what `AddAxisCloudflareR2Storage` registers
- [Custom adapter](custom-adapter.md) — implement `IAxisStorage` for your storage
- [Why AxisStorage?](why-axisstorage.md) — the case against direct `IAmazonS3`
- [API reference](api-reference.md) — every method in one place

---

↩ [Back to AxisStorage docs](README.md)
