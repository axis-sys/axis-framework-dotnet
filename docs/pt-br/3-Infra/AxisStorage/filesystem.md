# Adapter FileSystem · `AxisStorage.FileSystem`

> Uma implementação embarcada de `IAxisStorage` sustentada por um diretório em disco — disco local, um NAS montado, ou um file server montado. Implementa `IAxisStorageContainer` e `IAxisStorageLister`, mas **não** `IAxisStorageUrlResolver`: como um arquivo é servido a um cliente via HTTP é problema de quem serve o disco, não da porta de storage. O tipo do adapter é `internal sealed` — consuma-o via DI ou pela factory de destino-em-runtime `IFileSystemStorageFactory`, nunca por `new`.

```csharp
services.AddAxisFileSystemStorage(new FileSystemStorageSettings
{
    Root = "/var/lib/myapp/blobs",   // ou um caminho UNC/NAS montado
});
```

---

## Quando usar

Você quer o mesmo contrato `IAxisStorage` que seus handlers já consomem para R2/Azure, mas os bytes vivem num filesystem — disco local num container, um NAS montado, ou um file server. Chaves mapeiam para um caminho sob `Root` (`avatars/{id}.png` → `{Root}/avatars/{id}.png`), então o layout em disco espelha sua convenção de chaves. Também útil como backend de storage **real** em testes de integração, quando você não quer subir um bucket.

Este é um provider genérico de disco/NAS — pontos específicos de ambiente (servir arquivos via HTTP, URLs públicas de NAS) ficam de fora de propósito; veja [Quando *não* usar](#quando-não-usar).

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| mirar Cloudflare R2 / S3-compatível | o [`AxisStorage.CloudflareR2`](cloudflare-r2.md) da caixa |
| mirar Azure Blob Storage | o [`AxisStorage.AzureBlob`](azure-blob.md) da caixa |
| entregar ao cliente uma **URL servível pública/assinada** | este adapter não implementa [`IAxisStorageUrlResolver`](iaxisstorage.md#capacidades-opcionais--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver) — servir um arquivo via HTTP é trabalho de quem serve o disco (um web server, um controller fazendo streaming de `DownloadAsync`). Esse ponto de HTTP/base-URL é o ponto de extensão futuro pretendido |

---

## Settings — `FileSystemStorageSettings`

| Propriedade | Tipo | Descrição |
|---|---|---|
| `Root` | `string` (required) | o diretório base sob o qual toda chave é resolvida; criado sob demanda por `UploadAsync`/`EnsureExistsAsync` |

`FileSystemStorageSettings` é uma `public sealed class` com um único `required string Root`. Chaves usam separador `/` e são traduzidas para o separador de diretório da plataforma, então a mesma chave funciona no Windows e no Linux.

---

## O que é registrado

```csharp
services.AddSingleton(settings);
services.AddSingleton<FileSystemStorageAdapter>();
services.AddSingleton<IAxisStorage>(sp => sp.GetRequiredService<FileSystemStorageAdapter>());
services.AddSingleton<IAxisStorageContainer>(sp => sp.GetRequiredService<FileSystemStorageAdapter>());
services.AddSingleton<IAxisStorageLister>(sp => sp.GetRequiredService<FileSystemStorageAdapter>());
```

Uma única instância de `FileSystemStorageAdapter`, exposta sob as três interfaces que implementa — note que **não** há registro de `IAxisStorageUrlResolver`, diferente dos dois adapters de nuvem.

---

## Destino em runtime — `IFileSystemStorageFactory`

Quando o `Root` é escolhido em runtime (ex.: um diretório por tenant), registre a factory:

```csharp
builder.Services.AddAxisFileSystemStorageFactory();

// depois, num handler:
var storage = factory.Create(new FileSystemStorageSettings { Root = tenant.StorageRoot });
return storage.UploadAsync(key, content, contentType);
```

- `AddAxisFileSystemStorageFactory()` registra `IFileSystemStorageFactory` como **Singleton**.
- `Create(FileSystemStorageSettings destination)` retorna um `IAxisStorage`, cacheando um adapter por `Root`.
- A instância retornada também implementa `IAxisStorageContainer` / `IAxisStorageLister` — alcance-as por cast — mas não `IAxisStorageUrlResolver`.

---

## Como cada método mapeia para o filesystem

| `IAxisStorage` / `IAxisStorageContainer` / `IAxisStorageLister` | Operação de filesystem | Notas |
|---|---|---|
| `UploadAsync` | `Directory.CreateDirectory` + `File.Create` + `CopyToAsync` | cria os diretórios-pai ausentes; sobrescreve um arquivo existente |
| `DownloadAsync` | `File.OpenRead` | retorna uma stream de leitura que o chamador descarta |
| `DeleteAsync` | `File.Delete` (guardado por `File.Exists`) | idempotente — deletar chave ausente também tem sucesso |
| `ExistsAsync(key)` | `File.Exists` | booleano tipado |
| `GetPresignedUrlAsync` | `new Uri(path).AbsoluteUri` | retorna um URI `file://` — uma referência de caminho local, não uma URL HTTP |
| `IAxisStorageContainer.ExistsAsync()` | `Directory.Exists(Root)` | se o diretório raiz existe |
| `IAxisStorageContainer.EnsureExistsAsync()` | `Directory.CreateDirectory(Root)` | provisionamento idempotente da raiz |
| `IAxisStorageContainer.IsPubliclyAccessibleAsync()` | retorna `Ok(true)` | um filesystem não tem conceito de "URL assinada"; quem o serve decide o acesso real |
| `IAxisStorageLister.ListAsync(prefix)` | `Directory.EnumerateFiles(Root, "*", AllDirectories)` | retorna as keys (caminhos relativos, separados por `/`) que começam com `prefix` |

Todo método embrulha a operação em `AxisResult.TryAsync`, então qualquer exceção de I/O vira uma falha tipada de `AxisResult`, e o `CancellationToken` ambiente de `IAxisMediatorAccessor` é honrado.

---

## Por que sem `IAxisStorageUrlResolver`?

Um bucket de nuvem consegue cunhar uma URL que o navegador busca diretamente — crua e pública, ou assinada. Um filesystem não: um caminho `file://` não significa nada para um cliente remoto. Servir um arquivo armazenado a um cliente é responsabilidade de quem **serve** o disco (um controller que faz streaming de `DownloadAsync`, um reverse proxy, um NAS exposto via HTTP), então a capacidade é omitida de propósito aqui em vez de fingida. Se um deploy futuro precisar de URLs públicas de NAS/HTTP, a extensão pretendida é implementar `IAxisStorageUrlResolver` sobre uma base-URL configurável.

---

## Exemplo real — bind a partir da configuração

```csharp
// appsettings.json
// {
//   "Storage": { "Root": "/var/lib/myapp/blobs" }
// }

var settings = builder.Configuration.GetSection("Storage").Get<FileSystemStorageSettings>()!;
builder.Services.AddAxisFileSystemStorage(settings);
```

**Por que compensa:** a mesma superfície de cinco métodos que seus handlers consomem para R2/Azure agora funciona contra um disco ou NAS — sem mudança de código adiante, e os testes de integração ganham um backend real com zero rede.

---

## Veja também

- [O contrato `IAxisStorage`](iaxisstorage.md) — as cinco operações, mais as opcionais `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver`
- [Adapter Cloudflare R2](cloudflare-r2.md) — uma implementação de nuvem
- [Adapter Azure Blob](azure-blob.md) — a outra implementação de nuvem
- [Adapter custom](custom-adapter.md) — escrever um provider na mão

---

↩ [Voltar à documentação do AxisStorage](README.md)
