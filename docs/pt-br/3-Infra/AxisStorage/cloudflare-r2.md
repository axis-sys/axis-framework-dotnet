# Adapter Cloudflare R2 · `AxisStorage.CloudflareR2`

> Uma implementação embarcada de `IAxisStorage` — um `AmazonS3Client` configurado para o endpoint S3-compatível do Cloudflare R2, com um `CloudflareR2Settings` tipado na frente. Também implementa `IAxisStorageContainer`, `IAxisStorageLister` e `IAxisStorageUrlResolver`. O tipo do adapter é `internal sealed` — consuma-o via DI ou pela factory de destino-em-runtime `ICloudflareR2StorageFactory`, nunca por `new`.

```csharp
services.AddAxisCloudflareR2Storage(new CloudflareR2Settings
{
    AccountId  = "abc123",
    AccessKey  = "...",
    SecretKey  = "...",
    BucketName = "uploads",
    PublicUrl  = "https://cdn.example.com",   // opcional
});
```

---

## Quando usar

Você escolheu Cloudflare R2 (ou outro store S3-compatível alcançável via o SDK da AWS, como MinIO em testes). O adapter não assume features específicas do R2 — qualquer coisa que responda à API S3 funciona.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| mirar Azure Blob Storage | o [`AxisStorage.AzureBlob`](azure-blob.md) da caixa |
| mirar AWS S3 puro com endpoints regionais | um adapter custom usando `RegionEndpoint` diretamente |
| gravar em disco local / um NAS montado | o [`AxisStorage.FileSystem`](filesystem.md) da caixa |

---

## Settings — `CloudflareR2Settings`

| Propriedade | Tipo | Descrição |
|---|---|---|
| `AccountId` | `string` (required) | o account id do R2 — usado para construir o service URL `https://{AccountId}.r2.cloudflarestorage.com` |
| `AccessKey` | `string` (required) | a access key id do R2 |
| `SecretKey` | `string` (required) | a secret access key do R2 |
| `BucketName` | `string` (required) | o bucket que toda operação usa |
| `PublicUrl` | `string?` | prefixo opcional de URL pública (uma CDN na frente do bucket, ou o domínio `r2.dev` do bucket). Quando definido, `GetServableUrlAsync` retorna `{PublicUrl}/{key}` como uma **URL pública crua**; quando nulo, cai para uma URL **pré-assinada** — veja [URLs servíveis e `PublicUrl`](#urls-servíveis-e-publicurl) |
| `ServiceUrl` | `string` (computed) | `https://{AccountId}.r2.cloudflarestorage.com` — interno, usado para configurar o S3 client |

---

## O que é registrado

Lendo `DependencyInjection.AddAxisCloudflareR2Storage` direto:

```csharp
var s3Client = new AmazonS3Client(
    settings.AccessKey,
    settings.SecretKey,
    new AmazonS3Config
    {
        ServiceURL           = settings.ServiceUrl,            // endpoint do R2
        AuthenticationRegion = RegionEndpoint.USEast1.SystemName,
        ForcePathStyle       = true,                           // R2 exige path-style
    });

services.AddSingleton(settings);
services.AddSingleton<IAmazonS3>(s3Client);
services.AddSingleton<CloudflareR2StorageAdapter>();
services.AddSingleton<IAxisStorage>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
services.AddSingleton<IAxisStorageContainer>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
services.AddSingleton<IAxisStorageLister>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
services.AddSingleton<IAxisStorageUrlResolver>(sp => sp.GetRequiredService<CloudflareR2StorageAdapter>());
```

- `CloudflareR2Settings` é registrado como singleton.
- `IAmazonS3` é construído uma vez e registrado como singleton — connection pooling é cuidado dentro do SDK.
- Uma única instância de `CloudflareR2StorageAdapter` é exposta sob as quatro interfaces que implementa — `IAxisStorage`, `IAxisStorageContainer`, `IAxisStorageLister`, `IAxisStorageUrlResolver` — o mesmo formato de registro do `AxisStorage.AzureBlob`.

---

## Destino em runtime — `ICloudflareR2StorageFactory`

Para um destino escolhido em runtime (uma app multi-tenant com um bucket por tenant), registre a factory:

```csharp
builder.Services.AddAxisCloudflareR2StorageFactory();

// depois, num handler:
var storage = factory.Create(new CloudflareR2Settings
{
    AccountId  = tenant.AccountId,
    AccessKey  = tenant.AccessKey,
    SecretKey  = tenant.SecretKey,
    BucketName = tenant.Bucket,
    PublicUrl  = tenant.PublicUrl,
});
```

- `AddAxisCloudflareR2StorageFactory()` registra `ICloudflareR2StorageFactory` como **Singleton**.
- `Create(CloudflareR2Settings destination)` retorna um `IAxisStorage`, cacheando um adapter por destino (chaveado por `{AccountId}/{BucketName}`).
- Diferente da factory do Azure, `Create` recebe o `CloudflareR2Settings` **inteiro** por chamada, não só conta/container — as credenciais do R2 (`AccessKey`/`SecretKey`) são por bucket, então não há uma credencial única por processo para reusar.
- A instância retornada também implementa as capacidades opcionais — alcance-as por cast (`... as IAxisStorageUrlResolver`).

---

## Como cada método mapeia para `IAmazonS3`

| `IAxisStorage` / `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver` | Chamada no SDK AWS | Notas |
|---|---|---|
| `UploadAsync` | `PutObjectAsync(PutObjectRequest)` | faz streaming do corpo, define `Content-Type` |
| `DownloadAsync` | `GetObjectAsync(GetObjectRequest)` | retorna `ResponseStream` diretamente |
| `DeleteAsync` | `DeleteObjectAsync(DeleteObjectRequest)` | idempotente |
| `ExistsAsync(key)` | `GetObjectMetadataAsync(GetObjectMetadataRequest)` | captura `AmazonS3Exception` com `StatusCode == NotFound` e retorna `Ok(false)` |
| `GetPresignedUrlAsync` | `GetPreSignedURLAsync(GetPreSignedUrlRequest)` | `Verb = GET`, `Expires = UtcNow + expiration` |
| `IAxisStorageContainer.ExistsAsync()` | `GetBucketLocationAsync` | captura `AmazonS3Exception` com `StatusCode == NotFound` e retorna `Ok(false)` |
| `IAxisStorageContainer.EnsureExistsAsync()` | `PutBucketAsync` | tolera `ErrorCode == "BucketAlreadyOwnedByYou"` — idempotente |
| `IAxisStorageContainer.IsPubliclyAccessibleAsync()` | `GetBucketAclAsync` | `true` quando o grantee `AllUsers` tem `READ`; o Cloudflare R2 gerencia acesso público fora da API legada de ACL em algumas configurações — valide contra seu bucket antes de confiar nisso em produção |
| `IAxisStorageLister.ListAsync(prefix)` | `ListObjectsV2Async` (paginado via `ContinuationToken`) | retorna toda key sob `prefix` |
| `IAxisStorageUrlResolver.GetServableUrlAsync` | nenhuma (quando `PublicUrl` definido) / `GetPreSignedURLAsync` (senão) | retorna um `AxisStorageUrl` — veja abaixo |

Todo método embrulha a chamada do SDK em `AxisResult.TryAsync`, então qualquer exceção lançada vira uma falha tipada de `AxisResult`.

---

## URLs servíveis e `PublicUrl`

`GetServableUrlAsync(key, expiration)` implementa [`IAxisStorageUrlResolver`](iaxisstorage.md#capacidades-opcionais--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver) e é onde `CloudflareR2Settings.PublicUrl` finalmente ganha propósito — ele decide público-vs-assinado a partir desse único campo:

- **`PublicUrl` definido** → `{PublicUrl.TrimEnd('/')}/{key}`, uma URL pública crua (`IsPublic: true`, `ExpiresAt: null`). Sem chamada S3 — o R2 serve o objeto pela CDN/domínio `r2.dev` diretamente.
- **`PublicUrl` nulo/vazio** → uma URL pré-assinada (`IsPublic: false`, `ExpiresAt = now + expiration`), o mesmo caminho de assinatura de `GetPresignedUrlAsync`.

Diferente do adapter do Azure, o R2 toma a decisão pela configuração (`PublicUrl`), não por uma sonda de acesso público ao vivo — então aqui não há cache nem TTL.

---

## Exemplo real — bind a partir da configuração

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

**Por que compensa:** os segredos vivem na configuração (`appsettings`, environment, key vault). O adapter faz bind uma vez no startup, e seus handlers só veem `IAxisStorage`.

---

## Veja também

- [O contrato `IAxisStorage`](iaxisstorage.md) — as cinco operações, mais as opcionais `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver`
- [Upload / Download](upload-download.md) — o padrão de streaming
- [URLs pré-assinadas](presigned-urls.md) — o cunho de URL
- [Adapter Azure Blob](azure-blob.md) — a outra implementação de nuvem
- [Adapter FileSystem](filesystem.md) — a implementação em disco local / NAS
- [Adapter custom](custom-adapter.md) — embrulhe outro backend

---

↩ [Voltar à documentação do AxisStorage](README.md)
