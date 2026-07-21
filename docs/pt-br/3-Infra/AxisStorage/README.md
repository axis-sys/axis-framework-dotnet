# AxisStorage — Documentação

> 🌐 [English (README principal)](../../../en-us/3-Infra/AxisStorage/README.md)

**Uma porta de object-storage para C#** — `IAxisStorage` com cinco operações async (`Upload`, `Download`, `Delete`, `Exists`, `GetPresignedUrl`), todas retornando `AxisResult`. Três interfaces opcionais — `IAxisStorageContainer`, `IAxisStorageLister`, `IAxisStorageUrlResolver` — cobrem administração de container, listagem por prefixo e resolução de URL servível para os adapters que as suportam. Três adapters embarcados: `AxisStorage.CloudflareR2` liga o SDK do Amazon S3 ao Cloudflare R2 (o mesmo contrato serve para S3 puro, MinIO e qualquer outro bucket S3-compatível), `AxisStorage.AzureBlob` liga `Azure.Storage.Blobs` ao Azure Blob Storage — os dois implementam toda interface opcional — e `AxisStorage.FileSystem` grava em disco local ou num NAS montado (todas as opcionais exceto `IAxisStorageUrlResolver`).

```csharp
public Task<AxisResult<UploadAvatarResponse>> HandleAsync(UploadAvatarCommand cmd)
{
    var key = $"avatars/{cmd.PersonId}.png";

    return storage.UploadAsync(key, cmd.Content, "image/png")
        .ThenAsync(() => storage.GetPresignedUrlAsync(key, TimeSpan.FromHours(1)))
        .MapAsync(url => new UploadAvatarResponse { Url = url });
}
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### A interface em 60 segundos

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

Cinco métodos. Chaves são strings; conceitos do fornecedor (bucket, region, endpoint) vivem na configuração do **adapter**. Cancelamento flui de `IAxisMediatorAccessor`. → **[O contrato `IAxisStorage`](iaxisstorage.md)**

### Por que URL pré-assinada?

`GetPresignedUrlAsync` retorna uma URL com tempo de vida que o **cliente** pode usar para baixar o objeto direto do bucket — sem proxy pela sua API. Mais barato, mais rápido, e uma stream a menos para seu servidor segurar. → **[URLs pré-assinadas](presigned-urls.md)**

### Adapters embarcados — Cloudflare R2, Azure Blob e FileSystem

`AxisStorage.CloudflareR2` é um adapter do SDK do Amazon S3 plugado no endpoint do R2, com uma `CloudflareR2Settings` tipada (account id, chaves de acesso, bucket, URL pública opcional).

```csharp
services.AddAxisCloudflareR2Storage(new CloudflareR2Settings
{
    AccountId  = "...",
    AccessKey  = "...",
    SecretKey  = "...",
    BucketName = "uploads",
});
```

`AxisStorage.AzureBlob` é um adapter do `Azure.Storage.Blobs`, com uma `AzureBlobCredentialSettings` tipada (identidade Azure AD) e `AzureBlobSettings` (conta + container).

```csharp
services.AddAxisAzureBlobStorage(
    new AzureBlobCredentialSettings { /* vazio ⇒ DefaultAzureCredential ambiente */ },
    new AzureBlobSettings { AccountUrl = "https://myaccount.blob.core.windows.net", Container = "uploads" });
```

`AxisStorage.FileSystem` grava num diretório em disco (disco local, NAS montado, file server montado), com uma `FileSystemStorageSettings` tipada (`Root`).

```csharp
services.AddAxisFileSystemStorage(new FileSystemStorageSettings { Root = "/var/lib/myapp/blobs" });
```

→ **[Adapter Cloudflare R2](cloudflare-r2.md)** · **[Adapter Azure Blob](azure-blob.md)** · **[Adapter FileSystem](filesystem.md)**

Para um destino escolhido em runtime (multi-tenant), cada provider traz uma factory — `IAzureBlobStorageFactory`, `ICloudflareR2StorageFactory`, `IFileSystemStorageFactory` — registrada por `AddAxis…StorageFactory(...)`.

### Instalação

```
dotnet add package AxisStorage                  # a abstração
dotnet add package AxisStorage.CloudflareR2     # o adapter Cloudflare R2 (usa AWSSDK.S3)
dotnet add package AxisStorage.AzureBlob        # o adapter Azure Blob (usa Azure.Storage.Blobs)
dotnet add package AxisStorage.FileSystem       # o adapter disco / NAS (sem dependência extra)
```

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Contrato · `IAxisStorage`** | as cinco operações, mais as opcionais `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver` | [iaxisstorage.md](iaxisstorage.md) |
| **Upload / Download · `UploadAsync`, `DownloadAsync`** ⭐ | mover bytes para dentro e para fora do bucket | [upload-download.md](upload-download.md) |
| **URLs pré-assinadas · `GetPresignedUrlAsync`** | entregar ao cliente uma URL com tempo de vida | [presigned-urls.md](presigned-urls.md) |
| **Cloudflare R2 · `AxisStorage.CloudflareR2`** | um adapter embarcado | [cloudflare-r2.md](cloudflare-r2.md) |
| **Azure Blob · `AxisStorage.AzureBlob`** | outro adapter embarcado | [azure-blob.md](azure-blob.md) |
| **FileSystem · `AxisStorage.FileSystem`** | o adapter disco / NAS | [filesystem.md](filesystem.md) |
| **Adapter custom** | escreva outro (S3 puro, GCS) | [custom-adapter.md](custom-adapter.md) |
| **Por quê?** | o argumento contra `IAmazonS3` direto | [why-axisstorage.md](why-axisstorage.md) |
| **Referência** | cada método num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [O contrato `IAxisStorage`](iaxisstorage.md) · [Por que AxisStorage?](why-axisstorage.md)

**Fundamentos:** [Upload / Download](upload-download.md) · [URLs pré-assinadas](presigned-urls.md) · [Adapter Cloudflare R2](cloudflare-r2.md) · [Adapter Azure Blob](azure-blob.md) · [Adapter FileSystem](filesystem.md)

**Referência e extras:** [Adapter custom](custom-adapter.md) · [Referência da API](api-reference.md)

---

## Princípios de design

1. **Chaves vendor-neutral, config vendor-específica.** Código de aplicação fala em strings; bucket / region / endpoint vivem nas settings do adapter.
2. **Streams entram, streams saem.** Sem abstração que cacheia o blob inteiro — o contrato passa `Stream`, então uploads e downloads grandes funcionam sem OOM.
3. **`Exists` é um booleano tipado, não uma exceção.** `Ok(false)` para chave ausente; `IsFailure` só quando a chamada em si falhou.
4. **URLs pré-assinadas são first-class.** Downloads direto para o cliente economizam seu egress; são parte do contrato, não escape vendor.
5. **O SDK do fornecedor fica na borda.** Os adapters embarcados embrulham `IAmazonS3` e `BlobServiceClient` respectivamente, para que o código da aplicação nunca toque em `PutObjectRequest` ou `BlobClient` diretamente.
6. **Capacidades opcionais são interfaces separadas, não métodos opcionais.** Administração de container (`IAxisStorageContainer`), listagem por prefixo (`IAxisStorageLister`) e resolução de URL servível (`IAxisStorageUrlResolver`) vivem fora de `IAxisStorage`, para que um provider que não consiga suportar uma não seja forçado a fingir que suporta — o `AxisStorage.FileSystem`, por exemplo, implementa as duas primeiras mas não a terceira.

---

## Licença

Apache 2.0
