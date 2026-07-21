# FileSystem adapter · `AxisStorage.FileSystem`

> A bundled implementation of `IAxisStorage` backed by a directory on disk — local disk, a mounted NAS, or a mounted file server. It implements `IAxisStorageContainer` and `IAxisStorageLister`, but **not** `IAxisStorageUrlResolver`: how a file gets served to a client over HTTP is the business of whatever fronts the disk, not of the storage port. The adapter type is `internal sealed` — consume it through DI or the runtime-destination factory `IFileSystemStorageFactory`, never by `new`.

```csharp
services.AddAxisFileSystemStorage(new FileSystemStorageSettings
{
    Root = "/var/lib/myapp/blobs",   // or a mounted UNC/NAS path
});
```

---

## When to use

You want the same `IAxisStorage` contract your handlers already consume for R2/Azure, but the bytes live on a filesystem — local disk in a container, a mounted NAS, or a file server. Keys map to a path under `Root` (`avatars/{id}.png` → `{Root}/avatars/{id}.png`), so the on-disk layout mirrors your key convention. Also handy as a **real** storage backend in integration tests, where you don't want to spin up a bucket.

This is a generic disk/NAS provider — environment-specific concerns (serving files over HTTP, public NAS URLs) are deliberately left out; see [When *not* to use](#when-not-to-use).

## When *not* to use

| You want to… | Use instead |
|---|---|
| target Cloudflare R2 / S3-compatible | the in-box [`AxisStorage.CloudflareR2`](cloudflare-r2.md) |
| target Azure Blob Storage | the in-box [`AxisStorage.AzureBlob`](azure-blob.md) |
| hand a client a **servable public/signed URL** | this adapter does not implement [`IAxisStorageUrlResolver`](iaxisstorage.md#optional-capabilities--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver) — serving a file over HTTP is the job of whatever fronts the disk (a web server, a controller streaming `DownloadAsync`). That HTTP/base-URL concern is the intended future extension point |

---

## Settings — `FileSystemStorageSettings`

| Property | Type | Description |
|---|---|---|
| `Root` | `string` (required) | the base directory every key is resolved under; created on demand by `UploadAsync`/`EnsureExistsAsync` |

`FileSystemStorageSettings` is a `public sealed class` with a single `required string Root`. Keys use `/` separators and are translated to the platform's directory separator, so the same key works on Windows and Linux.

---

## What gets registered

```csharp
services.AddSingleton(settings);
services.AddSingleton<FileSystemStorageAdapter>();
services.AddSingleton<IAxisStorage>(sp => sp.GetRequiredService<FileSystemStorageAdapter>());
services.AddSingleton<IAxisStorageContainer>(sp => sp.GetRequiredService<FileSystemStorageAdapter>());
services.AddSingleton<IAxisStorageLister>(sp => sp.GetRequiredService<FileSystemStorageAdapter>());
```

One `FileSystemStorageAdapter` instance, exposed under the three interfaces it implements — note there is **no** `IAxisStorageUrlResolver` registration, unlike the two cloud adapters.

---

## Runtime destination — `IFileSystemStorageFactory`

When the `Root` is chosen at runtime (e.g. a directory per tenant), register the factory:

```csharp
builder.Services.AddAxisFileSystemStorageFactory();

// later, in a handler:
var storage = factory.Create(new FileSystemStorageSettings { Root = tenant.StorageRoot });
return storage.UploadAsync(key, content, contentType);
```

- `AddAxisFileSystemStorageFactory()` registers `IFileSystemStorageFactory` as a **Singleton**.
- `Create(FileSystemStorageSettings destination)` returns an `IAxisStorage`, caching one adapter per `Root`.
- The returned instance also implements `IAxisStorageContainer` / `IAxisStorageLister` — reach them by cast — but not `IAxisStorageUrlResolver`.

---

## How each method maps to the file system

| `IAxisStorage` / `IAxisStorageContainer` / `IAxisStorageLister` | File-system operation | Notes |
|---|---|---|
| `UploadAsync` | `Directory.CreateDirectory` + `File.Create` + `CopyToAsync` | creates any missing parent directories; overwrites an existing file |
| `DownloadAsync` | `File.OpenRead` | returns a read stream the caller disposes |
| `DeleteAsync` | `File.Delete` (guarded by `File.Exists`) | idempotent — deleting a missing key still succeeds |
| `ExistsAsync(key)` | `File.Exists` | typed boolean |
| `GetPresignedUrlAsync` | `new Uri(path).AbsoluteUri` | returns a `file://` URI — a local path reference, not an HTTP URL |
| `IAxisStorageContainer.ExistsAsync()` | `Directory.Exists(Root)` | whether the root directory exists |
| `IAxisStorageContainer.EnsureExistsAsync()` | `Directory.CreateDirectory(Root)` | idempotent provisioning of the root |
| `IAxisStorageContainer.IsPubliclyAccessibleAsync()` | returns `Ok(true)` | a filesystem has no "signed URL" concept; whoever fronts it decides real access |
| `IAxisStorageLister.ListAsync(prefix)` | `Directory.EnumerateFiles(Root, "*", AllDirectories)` | returns keys (relative paths, `/`-separated) that start with `prefix` |

Every method wraps the operation in `AxisResult.TryAsync`, so any I/O exception becomes a typed `AxisResult` failure, and the ambient `CancellationToken` from `IAxisMediatorAccessor` is honoured.

---

## Why no `IAxisStorageUrlResolver`?

A cloud bucket can mint a URL a browser fetches directly — either a raw public URL or a signed one. A filesystem cannot: a `file://` path is meaningless to a remote client. Serving a stored file to a client is the responsibility of whatever **fronts** the disk (a controller that streams `DownloadAsync`, a reverse proxy, a NAS exposed over HTTP), so the capability is deliberately omitted here rather than faked. If a deployment later needs public NAS/HTTP URLs, the intended extension is to implement `IAxisStorageUrlResolver` on top of a configurable base URL.

---

## Real-world example — bind from configuration

```csharp
// appsettings.json
// {
//   "Storage": { "Root": "/var/lib/myapp/blobs" }
// }

var settings = builder.Configuration.GetSection("Storage").Get<FileSystemStorageSettings>()!;
builder.Services.AddAxisFileSystemStorage(settings);
```

**Why it pays off:** the same five-method surface your handlers consume for R2/Azure now works against a disk or NAS — no code change downstream, and integration tests get a real backend with zero network.

---

## See also

- [The `IAxisStorage` contract](iaxisstorage.md) — the five operations, plus the optional `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver`
- [Cloudflare R2 adapter](cloudflare-r2.md) — a cloud implementation
- [Azure Blob adapter](azure-blob.md) — the other cloud implementation
- [Custom adapter](custom-adapter.md) — writing a provider by hand

---

↩ [Back to AxisStorage docs](README.md)
