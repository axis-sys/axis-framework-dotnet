# Adapter Azure Blob · `AxisStorage.AzureBlob`

> Uma implementação embarcada de `IAxisStorage` — um `BlobServiceClient` com um `AzureBlobSettings` tipado (destino) e `AzureBlobCredentialSettings` (a identidade da aplicação — uma credencial Azure AD, ou uma shared key para o emulador e contas acessadas por chave) na frente. Também implementa toda capacidade opcional: `IAxisStorageContainer`, `IAxisStorageLister` e `IAxisStorageUrlResolver`. O tipo do adapter em si é `internal sealed` — você o consome via DI (como `IAxisStorage` ou qualquer interface de capacidade) ou via a factory de destino-em-runtime `IAzureBlobStorageFactory`, nunca por `new`.

```csharp
services.AddAxisAzureBlobStorage(
    new AzureBlobCredentialSettings
    {
        // todos os campos vazios ⇒ DefaultAzureCredential ambiente (managed identity no Azure, `az login` local)
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

## Quando usar

Você está no Azure e quer o mesmo contrato `IAxisStorage` que seus handlers já consomem para R2/S3 — sem nenhum BC tocando `Azure.Storage.Blobs`, `Azure.Identity`, ou lógica de SAS/retry feita à mão.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| ficar em R2 / S3-compatível | o [`AxisStorage.CloudflareR2`](cloudflare-r2.md) da caixa |
| gravar em disco local / um NAS montado | o [`AxisStorage.FileSystem`](filesystem.md) da caixa |
| resolver uma conta/container **diferente** por chamada (ex.: uma conta Azure Storage por tenant) | registre `AddAxisAzureBlobStorageFactory(...)` e chame `IAzureBlobStorageFactory.Create(destino)` por requisição — `AddAxisAzureBlobStorage` registra um Singleton de destino único, a factory é o caminho multi-tenant (→ [Destino em runtime](#destino-em-runtime--iazureblobstoragefactory)) |

---

## Settings — `AzureBlobCredentialSettings` e `AzureBlobSettings`

Credencial e destino são dois objetos separados de propósito: a credencial é a **identidade da própria aplicação** (compartilhada entre todo container que a app toca), o destino é **onde** (conta + container) — assim uma única identidade atende vários containers, que é exatamente o que um storage multi-tenant precisa.

| Propriedade | Tipo | Descrição |
|---|---|---|
| `AzureBlobCredentialSettings.AccountName` | `string?` | nome da conta de storage para autenticação por **shared key** — quando definido junto com `AccountKey`, o par tem **precedência sobre toda a cadeia AAD** |
| `AzureBlobCredentialSettings.AccountKey` | `string?` | chave da conta de storage, par de `AccountName` |
| `AzureBlobCredentialSettings.TenantId` / `ClientId` / `ClientSecret` | `string?` | Service Principal — usados juntos, os três obrigatórios |
| `AzureBlobCredentialSettings.ManagedIdentityClientId` | `string?` | client id de uma Managed Identity atribuída pelo usuário |
| *(tudo vazio)* | | cai para `DefaultAzureCredential` ambiente — managed identity no Azure, `az login` local |
| `AzureBlobSettings.AccountUrl` | `string` (required) | ex.: `https://myaccount.blob.core.windows.net` |
| `AzureBlobSettings.Container` | `string` (required) | o container que toda operação usa |

Resolução de credencial (o seam `AzureBlobClients`): shared key (`AccountName`+`AccountKey` → `StorageSharedKeyCredential`) curto-circuita todo o resto; senão a cascata AAD (`AzureBlobCredential.Create`): Service Principal (`TenantId`+`ClientId`+`ClientSecret`) → `DefaultAzureCredential` restrito a `ManagedIdentityClientId` → `DefaultAzureCredential` puro.

> **Azurite / emulador:** o modo shared key existe exatamente para o emulador e para contas sem acesso ao Azure AD — defina `AccountName = "devstoreaccount1"` mais a chave conhecida do Azurite, e aponte `AzureBlobSettings.AccountUrl` para `http://127.0.0.1:10000/devstoreaccount1`.

---

## O que é registrado

```csharp
var options = new AzureBlobStorageOptions();
configure?.Invoke(options);                     // Action<AzureBlobStorageOptions> opcional
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

Uma única instância de `AzureBlobStorageAdapter`, exposta sob as quatro interfaces que implementa — resolva a que seu handler realmente precisa. O `AxisStorage.CloudflareR2` segue o mesmo formato de registro.

O `AzureBlobClients.ClientFactory` é o seam interno de credencial: ele devolve um construtor de `BlobServiceClient` carregando uma `StorageSharedKeyCredential` quando `AccountName`/`AccountKey` estão definidos, senão a `TokenCredential` da cascata AAD. No overload de factory (`AddAxisAzureBlobStorageFactory`) uma singleton de `TokenCredential` é registrada adicionalmente **só no caminho AAD** — uma composição shared-key não tem token credential para expor.

O `AddAxisAzureBlobStorage(credentialSettings, storageSettings, configure?)` recebe um `Action<AzureBlobStorageOptions>` opcional — veja [URLs servíveis e o TTL de acesso público](#urls-servíveis-e-o-ttl-de-acesso-público) abaixo.

---

## Destino em runtime — `IAzureBlobStorageFactory`

Quando a conta/container **não** é conhecida no startup — uma app multi-tenant que resolve `AccountUrl`/`Container` por requisição a partir do banco — registre a factory em vez de (ou ao lado de) o adapter de destino fixo:

```csharp
builder.Services.AddAxisAzureBlobStorageFactory(credentialSettings);   // uma credencial por processo

// depois, num handler, com um destino resolvido em runtime:
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

- `AddAxisAzureBlobStorageFactory(credentialSettings, configure?)` registra `IAzureBlobStorageFactory` como **Singleton**. A credencial é construída **uma vez por processo** (só o destino varia); o mesmo `Action<AzureBlobStorageOptions>` opcional se aplica.
- `Create(AzureBlobSettings destination)` retorna um `IAxisStorage`. A factory **cacheia um adapter por destino**, chaveado por `{AccountUrl}/{Container}`, então chamadas repetidas para o mesmo tenant reusam a mesma instância (e seus caches de delegation-key / acesso-público).
- A instância retornada também implementa as capacidades opcionais — alcance-as por cast: `factory.Create(dest) as IAxisStorageUrlResolver`, `... as IAxisStorageLister`.

Este é o substituto, dentro do framework, para fazer `new`-por-destino na mão no consumidor (o tipo do adapter é `internal`, então isso deixa de ser possível por design).

---

## Como cada método mapeia para o SDK do Azure

| `IAxisStorage` / `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver` | Chamada no SDK do Azure | Notas |
|---|---|---|
| `UploadAsync` | `BlobClient.UploadAsync(Stream, BlobUploadOptions)` | equivalente a `overwrite` por padrão; `BlobHttpHeaders.ContentType` definido na mesma chamada |
| `DownloadAsync` | `BlobClient.DownloadStreamingAsync` | retorna a response stream diretamente |
| `DeleteAsync` | `BlobClient.DeleteIfExistsAsync` | idempotente |
| `ExistsAsync(key)` | `BlobClient.ExistsAsync` | booleano tipado, nunca uma exceção 404 |
| `GetPresignedUrlAsync` | (AAD) `BlobServiceClient.GetUserDelegationKeyAsync` + `BlobSasBuilder` · (shared key) `BlobClient.GenerateSasUri` | dois branches de assinatura: um cliente AAD emite uma SAS delegada via Azure AD — nesse caminho a account key nunca sai do adapter, e a delegation key fica em cache por ~50 min; um cliente shared-key (`CanGenerateSasUri`) autoassina a SAS com a account key. Ambos concedem só Read |
| `IAxisStorageContainer.ExistsAsync()` | `BlobContainerClient.ExistsAsync` | nível de container, não de objeto |
| `IAxisStorageContainer.EnsureExistsAsync()` | `BlobContainerClient.CreateIfNotExistsAsync` | provisionamento idempotente |
| `IAxisStorageContainer.IsPubliclyAccessibleAsync()` | `BlobContainerClient.GetPropertiesAsync().PublicAccess` | `true` a menos que `PublicAccessType.None`; **sem** cache — sempre sonda ao vivo |
| `IAxisStorageLister.ListAsync(prefix)` | `BlobContainerClient.GetBlobsAsync(prefix:)` | pagina internamente, retorna os nomes de blob como keys |
| `IAxisStorageUrlResolver.GetServableUrlAsync` | (público) `Uri` do blob · (privado) mesmo caminho de SAS que `GetPresignedUrlAsync` | retorna um `AxisStorageUrl`: URL crua do blob quando o container é público, senão uma URL assinada. Consulta uma sonda de acesso-público **cacheada** (TTL abaixo) |

Todo método embrulha a chamada do SDK em `AxisResult.TryAsync`, com retry interno (backoff exponencial) para `RequestFailedException`/`AuthenticationFailedException` transientes — a resiliência que o chamador teria de escrever à mão em volta de cada chamada de blob, agora dentro do adapter.

---

## URLs servíveis e o TTL de acesso público

`GetServableUrlAsync(key, expiration)` é a implementação do adapter para [`IAxisStorageUrlResolver`](iaxisstorage.md#capacidades-opcionais--iaxisstoragecontainer-iaxisstoragelister-iaxisstorageurlresolver) — passe uma key e ele retorna a URL pronta que um cliente deve usar, sem você antes checar se o container é público:

- **Container público** → o `Uri` cru do blob (`IsPublic: true`, `ExpiresAt: null`), sem query string, sem expiração.
- **Container privado** → uma URL SAS (`IsPublic: false`, `ExpiresAt = now + expiration`), o mesmo caminho de assinatura de `GetPresignedUrlAsync` — delegada no AAD, autoassinada no shared key.

Para evitar um round-trip de `GetPropertiesAsync` a cada chamada, o adapter cacheia a resposta "este container é público?" por um TTL curto — **só** neste caminho do resolver; `IsPubliclyAccessibleAsync()` continua sondando ao vivo toda vez. O TTL é `AzureBlobStorageOptions.PublicAccessCacheTtl` (default **15 minutos**):

```csharp
builder.Services.AddAxisAzureBlobStorage(
    credentialSettings,
    storageSettings,
    options => options.PublicAccessCacheTtl = TimeSpan.FromMinutes(5));
```

`AzureBlobStorageOptions` é uma classe simples de settings (`public sealed class`, uma propriedade `TimeSpan PublicAccessCacheTtl`) e o mesmo argumento `configure` é aceito por `AddAxisAzureBlobStorageFactory`.

---

## Exemplo real — bind a partir da configuração

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

**Por que compensa:** só um lugar da app sabe da existência de `Azure.Storage.Blobs`/`Azure.Identity` — o composition root. Todo handler adiante enxerga só `IAxisStorage`.

---

## Veja também

- [O contrato `IAxisStorage`](iaxisstorage.md) — as cinco operações, mais as opcionais `IAxisStorageContainer` / `IAxisStorageLister` / `IAxisStorageUrlResolver`
- [Adapter Cloudflare R2](cloudflare-r2.md) — a outra implementação de nuvem
- [Adapter FileSystem](filesystem.md) — a implementação em disco local / NAS
- [Adapter custom](custom-adapter.md) — escrever um terceiro provider na mão

---

↩ [Voltar à documentação do AxisStorage](README.md)
