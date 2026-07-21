# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## O contrato — `IAxisStorage`

| Método | Assinatura | Descrição |
|---|---|---|
| `UploadAsync` | `Task<AxisResult> UploadAsync(string key, Stream content, string contentType)` | upload em streaming; sobrescreve silenciosamente |
| `DownloadAsync` | `Task<AxisResult<Stream>> DownloadAsync(string key)` | download em streaming; **chamador descarta** a stream |
| `DeleteAsync` | `Task<AxisResult> DeleteAsync(string key)` | remoção idempotente |
| `ExistsAsync` | `Task<AxisResult<bool>> ExistsAsync(string key)` | checagem de existência não-lançante |
| `GetPresignedUrlAsync` | `Task<AxisResult<string>> GetPresignedUrlAsync(string key, TimeSpan expiration)` | URL GET cunhada, válida por `now + expiration` |

→ [O contrato `IAxisStorage`](iaxisstorage.md) · [Upload / Download](upload-download.md) · [URLs pré-assinadas](presigned-urls.md)

---

## As interfaces opcionais — `IAxisStorageContainer`, `IAxisStorageLister`, `IAxisStorageUrlResolver`

| Método | Assinatura | Descrição |
|---|---|---|
| `ExistsAsync` | `Task<AxisResult<bool>> ExistsAsync()` | checagem de existência a nível de container/bucket |
| `EnsureExistsAsync` | `Task<AxisResult> EnsureExistsAsync()` | provisionamento idempotente de container/bucket |
| `IsPubliclyAccessibleAsync` | `Task<AxisResult<bool>> IsPubliclyAccessibleAsync()` | *reporta* se o container/bucket serve objetos sem URL assinada |
| `ListAsync` | `Task<AxisResult<IReadOnlyList<string>>> ListAsync(string prefix)` | toda key sob `prefix` |
| `GetServableUrlAsync` | `Task<AxisResult<AxisStorageUrl>> GetServableUrlAsync(string key, TimeSpan expiration)` | *decide e emite* a URL que um cliente deve usar — crua quando o container é público, assinada quando é privado |

`AxisStorageUrl` — `public sealed record AxisStorageUrl(string Url, bool IsPublic, DateTimeOffset? ExpiresAt)`: a `Url` resolvida, se é uma URL pública crua (`IsPublic`), e quando uma URL assinada expira (`ExpiresAt`, `null` quando pública).

→ [O contrato `IAxisStorage` — capacidades opcionais](iaxisstorage.md#capacidades-opcionais--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver)

---

## Adapter Cloudflare R2 — `AxisStorage.CloudflareR2`

| Membro | Descrição |
|---|---|
| `CloudflareR2Settings` | configuração tipada: `AccountId`, `AccessKey`, `SecretKey`, `BucketName`, `PublicUrl?` (dirige público-vs-assinado em `GetServableUrlAsync`) |
| `CloudflareR2StorageAdapter` | `internal sealed`; implementa `IAxisStorage` + `IAxisStorageContainer` + `IAxisStorageLister` + `IAxisStorageUrlResolver`; não instanciável pelo consumidor |
| `ICloudflareR2StorageFactory` | `IAxisStorage Create(CloudflareR2Settings destination)` — um adapter por destino para buckets escolhidos em runtime (settings inteiro por chamada, já que as credenciais do R2 são por bucket) |
| `services.AddAxisCloudflareR2Storage(settings)` | extensão DI; registra settings + `IAmazonS3` + o adapter sob `IAxisStorage`/`IAxisStorageContainer`/`IAxisStorageLister`/`IAxisStorageUrlResolver` (singletons, mesma instância) |
| `services.AddAxisCloudflareR2StorageFactory()` | extensão DI; registra `ICloudflareR2StorageFactory` (Singleton) |

→ [Adapter Cloudflare R2](cloudflare-r2.md)

---

## Adapter Azure Blob — `AxisStorage.AzureBlob`

| Membro | Descrição |
|---|---|
| `AzureBlobCredentialSettings` | configuração tipada de identidade: `TenantId?`, `ClientId?`, `ClientSecret?`, `ManagedIdentityClientId?` |
| `AzureBlobSettings` | configuração tipada de destino: `AccountUrl`, `Container` |
| `AzureBlobStorageOptions` | `public sealed class`; `TimeSpan PublicAccessCacheTtl` (default 15 min) — TTL da sonda de acesso-público cacheada no caminho de `GetServableUrlAsync` |
| `AzureBlobCredential.Create(AzureBlobCredentialSettings)` | resolver estático: Service Principal → `DefaultAzureCredential` restrito → `DefaultAzureCredential` puro |
| `AzureBlobStorageAdapter` | `internal sealed`; implementa `IAxisStorage` + `IAxisStorageContainer` + `IAxisStorageLister` + `IAxisStorageUrlResolver`; não instanciável pelo consumidor |
| `IAzureBlobStorageFactory` | `IAxisStorage Create(AzureBlobSettings destination)` — um adapter por destino (chaveado por `{AccountUrl}/{Container}`), reusando uma credencial por processo |
| `services.AddAxisAzureBlobStorage(credentialSettings, storageSettings, configure?)` | extensão DI; registra settings + `AzureBlobStorageOptions` + `BlobServiceClient` + o adapter sob `IAxisStorage`/`IAxisStorageContainer`/`IAxisStorageLister`/`IAxisStorageUrlResolver` (singletons, mesma instância). `configure` é um `Action<AzureBlobStorageOptions>` opcional |
| `services.AddAxisAzureBlobStorageFactory(credentialSettings, configure?)` | extensão DI; registra `IAzureBlobStorageFactory` (Singleton), uma credencial por processo |

→ [Adapter Azure Blob](azure-blob.md)

---

## Adapter FileSystem — `AxisStorage.FileSystem`

| Membro | Descrição |
|---|---|
| `FileSystemStorageSettings` | `public sealed class`; `required string Root` — o diretório base (disco local, NAS montado, ou file server montado) |
| `FileSystemStorageAdapter` | `internal sealed`; implementa `IAxisStorage` + `IAxisStorageContainer` + `IAxisStorageLister` — **não** `IAxisStorageUrlResolver`. `GetPresignedUrlAsync` retorna um URI `file://`; `IsPubliclyAccessibleAsync()` retorna `Ok(true)`; `EnsureExistsAsync()` cria o diretório |
| `IFileSystemStorageFactory` | `IAxisStorage Create(FileSystemStorageSettings destination)` — um adapter por `Root` |
| `services.AddAxisFileSystemStorage(settings)` | extensão DI; registra settings + o adapter sob `IAxisStorage`/`IAxisStorageContainer`/`IAxisStorageLister` (singletons, mesma instância) |
| `services.AddAxisFileSystemStorageFactory()` | extensão DI; registra `IFileSystemStorageFactory` (Singleton) |

→ [Adapter FileSystem](filesystem.md)

---

## Contrato de comportamento (para adapters)

| Operação | Estado do objeto | `AxisResult` retornado | Estado do objeto depois |
|---|---|---|---|
| `UploadAsync` | qualquer | `Ok()` | escrito/sobrescrito |
| `DownloadAsync` | existe | `Ok(stream)` | inalterado |
| `DownloadAsync` | ausente | `Error(...)` | inalterado |
| `DeleteAsync` | qualquer | `Ok()` | removido (se existia) |
| `ExistsAsync` | existe | `Ok(true)` | inalterado |
| `ExistsAsync` | ausente | `Ok(false)` | inalterado |
| `GetPresignedUrlAsync` | n/a (sem checagem de existência) | `Ok(url)` | inalterado |
| `GetServableUrlAsync` (opcional) | n/a (sem checagem de existência) | `Ok(AxisStorageUrl)` | inalterado |
| qualquer | n/a | adapter lançou | `Error(InternalServerError(...))` |
| qualquer | n/a | cancelado | *(lança `OperationCanceledException` — nenhum `AxisResult` é retornado)* |

> Cancelamento é a exceção deliberada à regra "storage nunca lança": um token ambiente cancelado se propaga como um `OperationCanceledException` real, não como um `AxisResult` de falha.

→ [Adapter custom](custom-adapter.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale, registre, faça upload
- [Por que AxisStorage?](why-axisstorage.md) — o argumento pela abstração
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisStorage](README.md)
