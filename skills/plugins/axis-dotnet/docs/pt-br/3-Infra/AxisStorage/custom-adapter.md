# Adapter custom · escreva seu próprio `IAxisStorage`

> Adicione um terceiro provider ao lado de `AxisStorage.CloudflareR2` e `AxisStorage.AzureBlob` — AWS S3 puro (não via R2), Google Cloud Storage, uma implementação em disco local para testes, ou um híbrido que lê de um backend e escreve em dois. Implemente cinco métodos, registre sua classe como `IAxisStorage`.

```csharp
public class GoogleCloudStorageAdapter(StorageClient client, string bucket, IAxisMediatorAccessor accessor) : IAxisStorage
{
    private CancellationToken Ct => accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;

    public Task<AxisResult> UploadAsync(string key, Stream content, string contentType)
        => AxisResult.TryAsync(async () =>
        {
            Ct.ThrowIfCancellationRequested();
            await client.UploadObjectAsync(bucket, key, contentType, content, cancellationToken: Ct);
        });

    // … DownloadAsync, DeleteAsync, ExistsAsync, GetPresignedUrlAsync
}
```

`AxisStorage.AzureBlob` (veja [sua própria página](azure-blob.md)) é um exemplo completo, não-ilustrativo, desse padrão — vale ler de ponta a ponta antes de escrever um terceiro provider.

---

## Quando usar

- AWS S3 puro (não via R2), Google Cloud Storage, ou qualquer outro object store.
- Um adapter de **disco local** para testes unitários.
- Um adapter mirror **lê-de-um, escreve-em-vários** para migração.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| mirar R2 / S3-compatível | o [`AxisStorage.CloudflareR2`](cloudflare-r2.md) da caixa |
| mirar Azure Blob Storage | o [`AxisStorage.AzureBlob`](azure-blob.md) da caixa |
| adicionar features de nível de bucket (versionamento, tagging) | estenda o contrato com uma nova interface, ao lado de `IAxisStorageContainer`/`IAxisStorageLister`/`IAxisStorageUrlResolver`, no mesmo pacote `AxisStorage`, e exija que o *adapter* a implemente (→ [capacidades opcionais](iaxisstorage.md#capacidades-opcionais--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver)) |
| resolver uma conta/container **diferente** por chamada (storage multi-tenant) | injete a factory do provider — `IAzureBlobStorageFactory` / `ICloudflareR2StorageFactory` / `IFileSystemStorageFactory` — e chame `Create(destino)`. Os adapters da caixa são `internal` por design e nunca são instanciados pelo consumidor (→ [Azure Blob · Destino em runtime](azure-blob.md#destino-em-runtime--iazureblobstoragefactory)) |

---

## O contrato que você precisa honrar

| Comportamento | Obrigatório | Razão |
|---|---|---|
| Todo método retorna `AxisResult`, nunca lança cooperativamente | sim | a ferrovia depende disso |
| `UploadAsync` sobrescreve silenciosamente | sim | espelha semântica S3; se precisar de if-not-exists, exponha um novo método |
| `DownloadAsync` retorna uma `Stream` que o chamador descarta | sim | streaming, não buffering |
| `ExistsAsync` retorna `Ok(false)` em chave ausente, **não** falha | sim | para chamadores poderem ramificar sem fluxo de exceção |
| `DeleteAsync` é idempotente | sim | espelha semântica S3 |
| Honre cancelamento de `IAxisMediatorAccessor.AxisMediator?.CancellationToken` | recomendado | espelha o adapter da caixa |
| Logue via `AxisLogger` | recomendado | enrichers anexam correlation / tenant |

---

## Exemplo real — adapter de disco local para testes

> Para storage **real** em disco ou NAS montado, não faça na mão — use o provider embarcado [`AxisStorage.FileSystem`](filesystem.md). O exemplo abaixo só ilustra o formato do contrato.

```csharp
public class LocalDiskStorageAdapter(string root, IAxisMediatorAccessor accessor) : IAxisStorage
{
    private CancellationToken Ct => accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;

    private string Path(string key) => System.IO.Path.Combine(root, key.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public Task<AxisResult> UploadAsync(string key, Stream content, string contentType)
        => AxisResult.TryAsync(async () =>
        {
            var path = Path(key);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            await using var fs = File.Create(path);
            await content.CopyToAsync(fs, Ct);
        });

    public Task<AxisResult<Stream>> DownloadAsync(string key)
        => AxisResult.TryAsync(() =>
        {
            Ct.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(File.OpenRead(Path(key)));
        });

    public Task<AxisResult> DeleteAsync(string key)
        => AxisResult.TryAsync(() =>
        {
            var path = Path(key);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        });

    public Task<AxisResult<bool>> ExistsAsync(string key)
        => AxisResult.TryAsync(() => Task.FromResult(File.Exists(Path(key))));

    public Task<AxisResult<string>> GetPresignedUrlAsync(string key, TimeSpan expiration)
        => AxisResult.TryAsync(() => Task.FromResult($"file://{Path(key)}"));
}
```

**Por que compensa:** testes de integração rodam com **zero rede**, o mesmo `IAxisStorage` que seu código de produção consome — e CI nunca precisa mockar o SDK da AWS na mão.

---

## Veja também

- [O contrato `IAxisStorage`](iaxisstorage.md) — a superfície que você precisa satisfazer
- [Adapter Cloudflare R2](cloudflare-r2.md) — uma referência da caixa
- [Adapter Azure Blob](azure-blob.md) — a outra referência da caixa, e a que copiar para um terceiro provider real (não-ilustrativo)
- [Adapter FileSystem](filesystem.md) — o provider de disco/NAS da caixa (antes de escrever seu próprio adapter de disco local)
- [Upload / Download](upload-download.md) — semântica de streaming
- [URLs pré-assinadas](presigned-urls.md) — cunho de URL

---

↩ [Voltar à documentação do AxisStorage](README.md)
