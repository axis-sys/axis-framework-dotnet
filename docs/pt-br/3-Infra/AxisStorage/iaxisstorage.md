# Contrato · `IAxisStorage`

> Uma porta vendor-neutral de object-storage. Cinco métodos async, todo resultado um `AxisResult`. Chaves são strings; bucket, region e endpoint são problema do adapter.

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

---

## Quando usar

Em qualquer lugar onde sua aplicação tenha que persistir ou ler **conteúdo binário** que não pertence a um banco relacional: imagens de avatar, anexos de documentos, exports, logs que você quer manter frios. Chaves espelham um caminho de arquivo (`avatars/{id}.png`), o resto é da sua convenção de nomenclatura.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| armazenar registros **estruturados** | um banco via [`AxisRepository`](../AxisRepository/README.md) |
| enviar um **anexo de email transacional** | resolva uma URL pré-assinada e passe ao [`AxisEmail`](../AxisEmail/README.md) |
| cachear algo | [`AxisCache`](../AxisCache/README.md) |
| hospedar uma CDN | uma CDN de verdade — `AxisStorage` é a origem |

---

## As cinco operações

| Método | Sucesso significa | `IsFailure` quando |
|---|---|---|
| `UploadAsync(key, stream, contentType)` | a stream foi escrita sob `key`, sobrescrevendo qualquer objeto existente | adapter lançou |
| `DownloadAsync(key)` | a stream está pronta para leitura | o objeto não existe ou o adapter lançou |
| `DeleteAsync(key)` | a chave sumiu (deletar chave ausente também é sucesso) | adapter lançou |
| `ExistsAsync(key)` | a chamada completou; `Value` diz se estava lá | adapter lançou num erro não-404 |
| `GetPresignedUrlAsync(key, expiration)` | uma URL assinada válida até `now + expiration` foi cunhada | adapter lançou |

> Cancelamento implícito: todo método no `CloudflareR2StorageAdapter` embarcado puxa o `CancellationToken` de `accessor.AxisMediator!.CancellationToken` e o passa para a chamada do SDK do S3. Um token ambiente cancelado é a única exceção deliberada à regra "storage nunca lança": ele surge como um `OperationCanceledException` real, nunca como um `AxisResult` com `IsFailure`.

---

## Capacidades opcionais — `IAxisStorageContainer`, `IAxisStorageLister`, `IAxisStorageUrlResolver`

`IAxisStorage` fica em cinco métodos de propósito — todo provider precisa implementar todos eles. Administração de container, listagem por prefixo e resolução de URL servível **não** são universais o bastante para forçar em todo adapter (nem sempre mapeiam de forma limpa para todo object store), então vivem em três interfaces separadas e opcionais, no mesmo pacote `AxisStorage`. Os dois adapters de nuvem (`AxisStorage.CloudflareR2`, `AxisStorage.AzureBlob`) implementam as três; o `AxisStorage.FileSystem` implementa as duas primeiras mas **não** `IAxisStorageUrlResolver` (como um arquivo em disco/NAS é servido via HTTP é problema de quem o serve, não da porta de storage); um adapter futuro é livre para implementar só `IAxisStorage`.

```csharp
public interface IAxisStorageContainer
{
    Task<AxisResult<bool>> ExistsAsync();
    Task<AxisResult>       EnsureExistsAsync();
    Task<AxisResult<bool>> IsPubliclyAccessibleAsync();
}

public interface IAxisStorageLister
{
    Task<AxisResult<IReadOnlyList<string>>> ListAsync(string prefix);
}

public interface IAxisStorageUrlResolver
{
    Task<AxisResult<AxisStorageUrl>> GetServableUrlAsync(string key, TimeSpan expiration);
}

public sealed record AxisStorageUrl(string Url, bool IsPublic, DateTimeOffset? ExpiresAt);
```

| Método | Sucesso significa | Use para |
|---|---|---|
| `IAxisStorageContainer.ExistsAsync()` | o bucket/container em si existe | checagem prévia antes de uma operação que assume que o container está lá |
| `IAxisStorageContainer.EnsureExistsAsync()` | o bucket/container existe depois da chamada (criado se ausente) | provisionamento explícito — chame no setup de tenant/ambiente, não implicitamente no primeiro upload |
| `IAxisStorageContainer.IsPubliclyAccessibleAsync()` | `Value` reflete se o bucket/container serve objetos sem URL assinada | decidir entre devolver uma URL pública crua ou um resultado de `GetPresignedUrlAsync` |
| `IAxisStorageLister.ListAsync(prefix)` | `Value` contém toda key sob `prefix` | jobs de import/reconciliação que precisam enumerar o que já está armazenado, não só buscar uma key conhecida |
| `IAxisStorageUrlResolver.GetServableUrlAsync(key, expiration)` | `Value` é o `AxisStorageUrl` pronto que um cliente deve usar — a URL pública crua quando o container serve objetos publicamente, senão uma URL assinada válida por `expiration` | entregar ao navegador uma única URL sem ramificar público-vs-privado você mesmo |

### `IsPubliclyAccessibleAsync` *reporta*; `GetServableUrlAsync` *decide e emite*

As duas parecem próximas mas respondem perguntas diferentes. `IAxisStorageContainer.IsPubliclyAccessibleAsync` **reporta um fato** — se o container hoje serve objetos sem assinatura — e deixa a ramificação com você. `IAxisStorageUrlResolver.GetServableUrlAsync` **decide e emite**: consulta esse mesmo fato e retorna a URL pronta — uma URL crua (`IsPublic: true`, `ExpiresAt: null`) quando o container é público, ou uma URL assinada (`IsPublic: false`, `ExpiresAt` em `now + expiration`) quando não é. O `AxisStorageUrl` retornado carrega os três, então o chamador não precisa rederivá-los.

Use o resolver quando você só quer a URL para devolver a um cliente; use o reporter quando precisa do fato cru para outra decisão. (No caminho do resolver, o adapter do Azure cacheia a resposta "é público" por um TTL curto e configurável — veja [Adapter Azure Blob](azure-blob.md).)

Por serem interfaces separadas, injete exatamente a que seu handler precisa — a maioria dos handlers só precisa de `IAxisStorage`; a capacidade de container / listagem / resolução de URL é para caminhos de código administrativos, de import, ou de servir download. `IAxisStorageUrlResolver` é ou injetado direto (os dois adapters de nuvem o registram no DI) ou obtido por cast de um `IAxisStorage` que você já tem — `storage as IAxisStorageUrlResolver` — que é como você o obtém de uma [factory de provider](azure-blob.md#destino-em-runtime--iazureblobstoragefactory) que devolve um `IAxisStorage` cru.

```csharp
public class ProvisionTenantStorageHandler(IAxisStorageContainer container) : IAxisCommandHandler<...>
{
    public Task<AxisResult> HandleAsync(ProvisionTenantStorageCommand cmd)
        => container.EnsureExistsAsync();
}
```

---

## Exemplos reais

### 1. Upload, depois devolva uma URL de download

```csharp
public Task<AxisResult<UploadAvatarResponse>> HandleAsync(UploadAvatarCommand cmd)
{
    var key = $"avatars/{cmd.PersonId}.png";

    return storage.UploadAsync(key, cmd.Content, "image/png")
        .ThenAsync(() => storage.GetPresignedUrlAsync(key, TimeSpan.FromHours(1)))
        .MapAsync(url => new UploadAvatarResponse { Url = url });
}
```

**Por que compensa:** a ferrovia carrega um modo de falha pelos dois passos. Se o upload falha, a URL nunca é cunhada; se o cunho da URL falha, o upload já está commitado e um `AxisError` carrega o motivo.

### 2. Delete idempotente

```csharp
public Task<AxisResult> RemoveAvatarAsync(AxisEntityId personId)
    => storage.DeleteAsync($"avatars/{personId}.png");
```

**Por que compensa:** `DeleteAsync` tem sucesso quer a chave existisse ou não — chamadores não precisam de `ExistsAsync` antes. Idempotente em nível de contrato.

### 3. Exists-antes-download para arquivos grandes

```csharp
public async Task<AxisResult<Stream>> GetExportAsync(AxisEntityId exportId)
{
    var key = $"exports/{exportId}.csv";

    var exists = await storage.ExistsAsync(key);
    if (exists.IsFailure || !exists.Value)
        return AxisError.NotFound("EXPORT_NOT_FOUND");

    return await storage.DownloadAsync(key);
}
```

**Por que compensa:** o `Ok(false)` tipado evita uma exceção 404 do SDK num caminho sabidamente ausente, e o handler lê limpo, sem ramo dirigido por exceção.

---

## Veja também

- [Upload / Download](upload-download.md) — os workhorses de streaming
- [URLs pré-assinadas](presigned-urls.md) — downloads direto para o cliente
- [Adapter Cloudflare R2](cloudflare-r2.md) — a implementação na caixa
- [Adapter Azure Blob](azure-blob.md) — a outra implementação na caixa
- [Adapter custom](custom-adapter.md) — implemente `IAxisStorage` para seu storage
- [Referência da API](api-reference.md) — cada método, num só lugar

---

↩ [Voltar à documentação do AxisStorage](README.md)
