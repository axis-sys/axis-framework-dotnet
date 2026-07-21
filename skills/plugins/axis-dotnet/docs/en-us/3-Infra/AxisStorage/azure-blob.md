# Azure Blob adapter · `AxisStorage.AzureBlob`

> A bundled implementation of `IAxisStorage` — a `BlobServiceClient` fronted by a typed `AzureBlobSettings` (destination) and `AzureBlobCredentialSettings` (the application's identity — an Azure AD credential, or a shared key for the emulator and key-accessed accounts). It also implements every optional capability: `IAxisStorageContainer`, `IAxisStorageLister`, and `IAxisStorageUrlResolver`. The adapter type itself is `internal sealed` — you consume it through DI (as `IAxisStorage` or any capability interface) or through the runtime-destination factory `IAzureBlobStorageFactory`, never by `new`.

```csharp
services.AddAxisAzureBlobStorage(
    new AzureBlobCredentialSettings
    {
        // all fields empty ⇒ ambient DefaultAzureCredential (managed identity in Azure, `az login` locally)
        TenantId     = "...",
        ClientId     = "...",
        ClientSecret = "...",
    },
    new AzureBlobSettings
    {
        AccountUrl = "https://myaccount.blob.core.windows.net",
        Container  = "uploads",
    });
```

---

## When to use

You are on Azure and want the same `IAxisStorage` contract your handlers already consume for R2/S3 — without any BC touching `Azure.Storage.Blobs`, `Azure.Identity`, or hand-rolled SAS/retry logic.

## When *not* to use

| You want to… | Use instead |
|---|---|
| stay on R2 / S3-compatible | the in-box [`AxisStorage.CloudflareR2`](cloudflare-r2.md) |
| store to local disk / a mounted NAS | the in-box [`AxisStorage.FileSystem`](filesystem.md) |
| resolve a **different** account/container per call (e.g. one Azure Storage account per tenant) | register `AddAxisAzureBlobStorageFactory(...)` and call `IAzureBlobStorageFactory.Create(destination)` per request — `AddAxisAzureBlobStorage` registers a single-destination Singleton, the factory is the multi-tenant path (→ [Runtime destination](#runtime-destination--iazureblobstoragefactory)) |

---

## Settings — `AzureBlobCredentialSettings` and `AzureBlobSettings`

Credential and destination are two separate objects on purpose: the credential is the **application's own identity** (shared across every container the app touches), the destination is **where** (account + container) — so a single identity can serve many containers, which is exactly what multi-tenant blob storage needs.

| Property | Type | Description |
|---|---|---|
| `AzureBlobCredentialSettings.AccountName` | `string?` | storage account name for **shared-key** authentication — when set together with `AccountKey`, the pair takes **precedence over the entire AAD chain** |
| `AzureBlobCredentialSettings.AccountKey` | `string?` | storage account key paired with `AccountName` |
| `AzureBlobCredentialSettings.TenantId` / `ClientId` / `ClientSecret` | `string?` | Service Principal — used together, all three required |
| `AzureBlobCredentialSettings.ManagedIdentityClientId` | `string?` | user-assigned Managed Identity client id |
| *(all empty)* | | falls back to ambient `DefaultAzureCredential` — managed identity in Azure, `az login` locally |
| `AzureBlobSettings.AccountUrl` | `string` (required) | e.g. `https://myaccount.blob.core.windows.net` |
| `AzureBlobSettings.Container` | `string` (required) | the container every operation uses |

Credential resolution (the `AzureBlobClients` seam): shared key (`AccountName`+`AccountKey` → `StorageSharedKeyCredential`) short-circuits everything else; otherwise the AAD cascade (`AzureBlobCredential.Create`): Service Principal (`TenantId`+`ClientId`+`ClientSecret`) → `DefaultAzureCredential` scoped to `ManagedIdentityClientId` → plain `DefaultAzureCredential`.

> **Azurite / emulator:** the shared-key mode exists precisely for the emulator and for accounts without Azure AD access — set `AccountName = "devstoreaccount1"` plus the well-known Azurite key, and point `AzureBlobSettings.AccountUrl` at `http://127.0.0.1:10000/devstoreaccount1`.

---

## What gets registered

```csharp
var options = new AzureBlobStorageOptions();
configure?.Invoke(options);                     // optional Action<AzureBlobStorageOptions>
var serviceClient = AzureBlobClients.ClientFactory(credentialSettings)(storageSettings.AccountUrl);

services.AddSingleton(options);
services.AddSingleton(storageSettings);
services.AddSingleton(serviceClient);
services.AddSingleton<AzureBlobStorageAdapter>();
services.AddSingleton<IAxisStorage>(sp => sp.GetRequiredService<AzureBlobStorageAdapter>());
services.AddSingleton<IAxisStorageContainer>(sp => sp.GetRequiredService<AzureBlobStorageAdapter>());
services.AddSingleton<IAxisStorageLister>(sp => sp.GetRequiredService<AzureBlobStorageAdapter>());
services.AddSingleton<IAxisStorageUrlResolver>(sp => sp.GetRequiredService<AzureBlobStorageAdapter>());
```

One `AzureBlobStorageAdapter` instance, exposed under all four interfaces it implements — resolve whichever your handler actually needs. `AxisStorage.CloudflareR2` follows the same registration shape.

`AzureBlobClients.ClientFactory` is the internal credential seam: it hands back a `BlobServiceClient` builder carrying a `StorageSharedKeyCredential` when `AccountName`/`AccountKey` are set, otherwise the `TokenCredential` from the AAD cascade. On the factory overload (`AddAxisAzureBlobStorageFactory`) a `TokenCredential` singleton is additionally registered **only on the AAD path** — a shared-key composition has no token credential to expose.

`AddAxisAzureBlobStorage(credentialSettings, storageSettings, configure?)` takes an optional `Action<AzureBlobStorageOptions>` — see [Servable URLs and the public-access TTL](#servable-urls-and-the-public-access-ttl) below.

---

## Runtime destination — `IAzureBlobStorageFactory`

When the account/container is **not** known at startup — a multi-tenant app that resolves `AccountUrl`/`Container` per request from the database — register the factory instead of (or alongside) the fixed-destination adapter:

```csharp
builder.Services.AddAxisAzureBlobStorageFactory(credentialSettings);   // one credential per process

// later, in a handler, with a destination resolved at runtime:
public Task<AxisResult> HandleAsync(UploadTenantFileCommand cmd)
{
    var storage = factory.Create(new AzureBlobSettings
    {
        AccountUrl = cmd.TenantAccountUrl,
        Container  = cmd.TenantContainer,
    });
    return storage.UploadAsync(cmd.Key, cmd.Content, cmd.ContentType);
}
```

- `AddAxisAzureBlobStorageFactory(credentialSettings, configure?)` registers `IAzureBlobStorageFactory` as a **Singleton**. The credential is built **once per process** (only the destination varies); the same optional `Action<AzureBlobStorageOptions>` applies.
- `Create(AzureBlobSettings destination)` returns an `IAxisStorage`. The factory **caches one adapter per destination**, keyed by `{AccountUrl}/{Container}`, so repeated calls for the same tenant reuse the same instance (and its delegation-key / public-access caches).
- The returned instance also implements the optional capabilities — reach them by cast: `factory.Create(dest) as IAxisStorageUrlResolver`, `... as IAxisStorageLister`.

This is the framework-owned replacement for hand-rolling `new`-per-destination in the consumer (the adapter type is `internal`, so that is no longer possible by design).

---

## How each method maps to the Azure SDK

| `IAxisStorage` / `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver` | Azure SDK call | Notes |
|---|---|---|
| `UploadAsync` | `BlobClient.UploadAsync(Stream, BlobUploadOptions)` | `overwrite`-equivalent by default; `BlobHttpHeaders.ContentType` set in the same call |
| `DownloadAsync` | `BlobClient.DownloadStreamingAsync` | returns the response stream directly |
| `DeleteAsync` | `BlobClient.DeleteIfExistsAsync` | idempotent |
| `ExistsAsync(key)` | `BlobClient.ExistsAsync` | typed boolean, never a 404 exception |
| `GetPresignedUrlAsync` | (AAD) `BlobServiceClient.GetUserDelegationKeyAsync` + `BlobSasBuilder` · (shared key) `BlobClient.GenerateSasUri` | two signing branches: an AAD client mints an Azure AD–delegated SAS — on that path no storage account key ever leaves the adapter, and the delegation key is cached ~50 min; a shared-key client (`CanGenerateSasUri`) self-signs the SAS with the account key. Both grant Read only |
| `IAxisStorageContainer.ExistsAsync()` | `BlobContainerClient.ExistsAsync` | container-level, not object-level |
| `IAxisStorageContainer.EnsureExistsAsync()` | `BlobContainerClient.CreateIfNotExistsAsync` | idempotent provisioning |
| `IAxisStorageContainer.IsPubliclyAccessibleAsync()` | `BlobContainerClient.GetPropertiesAsync().PublicAccess` | `true` unless `PublicAccessType.None`; **not** cached — always probes |
| `IAxisStorageLister.ListAsync(prefix)` | `BlobContainerClient.GetBlobsAsync(prefix:)` | paginates internally, returns blob names as keys |
| `IAxisStorageUrlResolver.GetServableUrlAsync` | (public) blob `Uri` · (private) same SAS path as `GetPresignedUrlAsync` | returns an `AxisStorageUrl`: raw blob URL when the container is public, else a signed URL. Consults a **cached** public-access probe (TTL below) |

Every method wraps the SDK call in `AxisResult.TryAsync`, with an internal retry (exponential backoff) for transient `RequestFailedException`/`AuthenticationFailedException` — the resilience a caller would otherwise hand-roll around every blob call, now inside the adapter.

---

## Servable URLs and the public-access TTL

`GetServableUrlAsync(key, expiration)` is the adapter's implementation of [`IAxisStorageUrlResolver`](iaxisstorage.md#optional-capabilities--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver) — hand it a key and it returns the finished URL a client should use, without you first checking whether the container is public:

- **Public container** → the raw blob `Uri` (`IsPublic: true`, `ExpiresAt: null`), no query string, no expiry.
- **Private container** → a SAS URL (`IsPublic: false`, `ExpiresAt = now + expiration`), the same signing path as `GetPresignedUrlAsync` — delegated on AAD, self-signed on shared key.

To avoid a `GetPropertiesAsync` round-trip on every call, the adapter caches the "is this container public?" answer for a short TTL — **only** on this resolver path; `IsPubliclyAccessibleAsync()` still probes live every time. The TTL is `AzureBlobStorageOptions.PublicAccessCacheTtl` (default **15 minutes**):

```csharp
builder.Services.AddAxisAzureBlobStorage(
    credentialSettings,
    storageSettings,
    options => options.PublicAccessCacheTtl = TimeSpan.FromMinutes(5));
```

`AzureBlobStorageOptions` is a plain settings class (`public sealed class`, one `TimeSpan PublicAccessCacheTtl` property) and the same `configure` argument is accepted by `AddAxisAzureBlobStorageFactory`.

---

## Real-world example — bind from configuration

```csharp
// appsettings.json
// {
//   "Storage": {
//     "Credential": { "TenantId": "...", "ClientId": "...", "ClientSecret": "..." },
//     "Destination": { "AccountUrl": "https://myaccount.blob.core.windows.net", "Container": "uploads" }
//   }
// }

var credentialSettings = builder.Configuration.GetSection("Storage:Credential").Get<AzureBlobCredentialSettings>()!;
var storageSettings    = builder.Configuration.GetSection("Storage:Destination").Get<AzureBlobSettings>()!;
builder.Services.AddAxisAzureBlobStorage(credentialSettings, storageSettings);
```

**Why it pays off:** exactly one place in the app knows about `Azure.Storage.Blobs`/`Azure.Identity` — the composition root. Every handler downstream sees `IAxisStorage`.

---

## See also

- [The `IAxisStorage` contract](iaxisstorage.md) — the five operations, plus the optional `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver`
- [Cloudflare R2 adapter](cloudflare-r2.md) — the other cloud implementation
- [FileSystem adapter](filesystem.md) — the local-disk / NAS implementation
- [Custom adapter](custom-adapter.md) — writing a third provider by hand

---

↩ [Back to AxisStorage docs](README.md)
