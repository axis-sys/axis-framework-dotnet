# Primeiros passos · instalação e uso

> Instale a abstração e o adapter Cloudflare R2, registre na DI e faça seu primeiro upload em cinco minutos.

---

## Instalação

```
dotnet add package AxisStorage                  # a abstração
dotnet add package AxisStorage.CloudflareR2     # o adapter Cloudflare R2
```

`AxisStorage` depende apenas de `AxisResult`. `AxisStorage.CloudflareR2` adiciona `AWSSDK.S3` (o R2 fala a API S3).

---

## Configurando o R2

R2 pede quatro segredos mais o nome do bucket. Coloque-os no `appsettings.json`:

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

## Registrando o adapter

```csharp
using AxisStorage.CloudflareR2;

var settings = builder.Configuration.GetSection("Storage").Get<CloudflareR2Settings>()!;
builder.Services.AddAxisCloudflareR2Storage(settings);
```

`AddAxisCloudflareR2Storage` constrói um `AmazonS3Client` contra o endpoint do R2 (`https://{AccountId}.r2.cloudflarestorage.com`), registra as settings como singleton e faz o bind `IAxisStorage → CloudflareR2StorageAdapter`.

---

## Fazendo upload

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

## Fazendo download

```csharp
public Task<AxisResult<Stream>> GetAvatarAsync(AxisEntityId personId)
    => storage.DownloadAsync($"avatars/{personId}.png");
```

---

## Existência e deleção

```csharp
var exists = await storage.ExistsAsync($"avatars/{personId}.png");

if (exists.IsSuccess && exists.Value)
    await storage.DeleteAsync($"avatars/{personId}.png");
```

**Por que compensa:** a mesma superfície de cinco métodos funciona para R2 hoje, S3 puro amanhã, MinIO em testes — seus handlers não mudam.

---

## Veja também

- [O contrato `IAxisStorage`](iaxisstorage.md) — cada método, semântica, modos de falha
- [Upload / Download](upload-download.md) — os workhorses de streaming
- [URLs pré-assinadas](presigned-urls.md) — downloads direto para o cliente
- [Adapter Cloudflare R2](cloudflare-r2.md) — o que `AddAxisCloudflareR2Storage` registra
- [Adapter custom](custom-adapter.md) — implemente `IAxisStorage` para seu storage
- [Por que AxisStorage?](why-axisstorage.md) — o argumento contra `IAmazonS3` direto
- [Referência da API](api-reference.md) — cada método num só lugar

---

↩ [Voltar à documentação do AxisStorage](README.md)
