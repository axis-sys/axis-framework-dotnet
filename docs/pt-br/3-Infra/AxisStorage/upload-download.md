# Upload / Download · `UploadAsync`, `DownloadAsync`

> Os dois workhorses. **Streams entram, streams saem** — o contrato nunca cacheia o objeto inteiro, então uploads e downloads de muitos gigabytes passam sem OOM.

```csharp
// upload
await using var fileStream = file.OpenReadStream();
await storage.UploadAsync($"exports/{exportId}.csv", fileStream, "text/csv");

// download
var download = await storage.DownloadAsync($"exports/{exportId}.csv");
if (download.IsSuccess)
    await download.Value.CopyToAsync(httpResponseStream);
```

---

## Quando usar

Qualquer hora que precise mover bytes entre seu app e o bucket. Os métodos aceitam e devolvem `Stream`, então você os pluga em arquivos, corpos de requisição HTTP, respostas HTTP, qualquer coisa que fale stream.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| entregar ao cliente uma URL que ele baixe direto | [`GetPresignedUrlAsync`](presigned-urls.md) |
| checar se algo está lá antes de baixar | [`ExistsAsync`](iaxisstorage.md) |
| remover um objeto | [`DeleteAsync`](iaxisstorage.md) |

---

## Semântica de upload

| Comportamento | Adapter |
|---|---|
| Sobrescreve chave existente silenciosamente | sim — não há modo "if-not-exists" no contrato |
| Lê `content` uma vez, sequencialmente | sim — passar uma stream forward-only é OK |
| Define `Content-Type` no objeto | sim — argumento obrigatório, sem default |
| Retorna `Ok()` no sucesso | sim — sem metadados de volta; consulte `ExistsAsync` se precisar confirmar |

## Semântica de download

| Comportamento | Adapter |
|---|---|
| Retorna o corpo como `Stream` forward-only | sim |
| **Chamador é dono da stream** | sim — embrulhe em `await using` para descartar |
| Retorna `IsFailure` em chave ausente | sim (404 do R2/S3 sobe como exceção → `AxisResult.TryAsync` converte) |

> **Borda afiada:** a `Stream` retornada é propriedade do chamador. Sempre embrulhe a chamada em `await using` (ou passe a stream para algo que descarta) ou você vai vazar uma conexão.

---

## Exemplos reais

### 1. Recebendo um upload de arquivo HTTP

```csharp
[HttpPost("/api/files")]
public async Task<IResult> UploadAsync([FromForm] IFormFile file, IAxisStorage storage)
{
    if (file.Length == 0) return Results.BadRequest("EMPTY_FILE");

    var key = $"uploads/{Guid.CreateVersion7()}/{file.FileName}";

    await using var stream = file.OpenReadStream();
    var result = await storage.UploadAsync(key, stream, file.ContentType);

    return result.IsSuccess
        ? Results.Created($"/api/files/{key}", new { key })
        : Results.Problem(result.Errors[0].Code);
}
```

**Por que compensa:** o upload faz streaming direto do corpo da requisição para o bucket — sem `MemoryStream`, sem arquivo temporário, sem cópia em nível de kernel.

### 2. Servindo download para a stream da resposta

```csharp
[HttpGet("/api/exports/{exportId}")]
public async Task<IResult> DownloadAsync(AxisEntityId exportId, IAxisStorage storage)
{
    var key = $"exports/{exportId}.csv";
    var result = await storage.DownloadAsync(key);

    if (result.IsFailure)
        return Results.NotFound();

    return Results.Stream(result.Value, "text/csv", $"{exportId}.csv");
}
```

**Por que compensa:** `Results.Stream` descarta a stream quando a resposta termina. Os bytes fluem do R2 para o cliente sem buffer no seu processo.

### 3. Pipe servidor-a-servidor

```csharp
public async Task<AxisResult> CopyTo(string sourceKey, string destKey)
{
    var download = await storage.DownloadAsync(sourceKey);
    if (download.IsFailure) return download.ToAxisResult();

    await using var src = download.Value;
    return await storage.UploadAsync(destKey, src, "application/octet-stream");
}
```

**Por que compensa:** uma cópia em uma passada. A stream de download alimenta direto o upload — seu processo nunca segura o objeto em memória ou disco.

---

## Veja também

- [O contrato `IAxisStorage`](iaxisstorage.md) — cada método
- [URLs pré-assinadas](presigned-urls.md) — deixe o cliente pular o proxy
- [Adapter Cloudflare R2](cloudflare-r2.md) — como o adapter embarcado implementa

---

↩ [Voltar à documentação do AxisStorage](README.md)
