# URLs pré-assinadas · `GetPresignedUrlAsync`

> Cunhe uma **URL com tempo de vida** que o cliente pode usar para baixar o objeto direto do bucket. Sem proxy pela sua API, sem egress nos seus servidores de aplicação e uma stream a menos para seu processo cuidar.

```csharp
var result = await storage.GetPresignedUrlAsync(
    key:        $"exports/{exportId}.csv",
    expiration: TimeSpan.FromMinutes(15));

if (result.IsSuccess)
    return new DownloadResponse { Url = result.Value };
```

---

## Quando usar

- O cliente (browser, app mobile) pode falar com o bucket diretamente.
- O objeto é grande o suficiente para que o proxy pela sua aplicação domine sua conta de egress.
- Você quer expirar acesso automaticamente — distribuir uma URL que para de funcionar em 15 minutos é a forma mais simples de autorização.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| fazer streaming dos bytes pela sua aplicação (auth, transformação, marca d'água) | [`DownloadAsync`](upload-download.md) |
| manter uma URL pública permanente | configure uma política de read público no nível do bucket |
| fazer upload pelo cliente | uma URL pré-assinada de **PUT** — o adapter embarcado implementa só GET |

---

## Comportamento

| Aspecto | Comportamento |
|---|---|
| Verbo HTTP | `GET` (o adapter embarcado chama `GetPreSignedURLAsync` com `Verb = HttpVerb.GET`) |
| Expiração | `now + expiration` — passado como `DateTime.UtcNow.Add(expiration)` |
| Bucket | o configurado em `CloudflareR2Settings.BucketName` |
| Existência do objeto | **não** checada — uma URL é cunhada quer a chave exista ou não; o **cliente** vê o 404 quando tenta baixar uma chave ausente |
| Cancelamento | honrado (a chamada do SDK aceita o token ambiente) |

> **Borda afiada:** não há parâmetro `Verb`/`Method` no contrato. O adapter embarcado cunha **GET** URLs apenas. Para uploads, escreva uma pequena extensão no seu adapter ou exponha uma abstração mais rica.

---

## Exemplos reais

### 1. Download de avatar via URL assinada

```csharp
public Task<AxisResult<GetAvatarUrlResponse>> HandleAsync(GetAvatarUrlQuery query)
    => storage.GetPresignedUrlAsync(
            key:        $"avatars/{query.PersonId}.png",
            expiration: TimeSpan.FromHours(1))
        .MapAsync(url => new GetAvatarUrlResponse { Url = url });
```

**Por que compensa:** o cliente recebe uma URL que ele cacheia por uma hora. Sua API é batida uma vez por renovação, não por imagem. O browser puxa do R2 via CDN, não da sua origem.

### 2. Envie um link de export por email

```csharp
public Task<AxisResult> NotifyExportReadyAsync(ExportReadyEvent @event)
    => storage.GetPresignedUrlAsync($"exports/{@event.ExportId}.zip", TimeSpan.FromDays(1))
        .ThenAsync(url => email.SendAsync(new ExportReadyMessage(@event.UserEmail, url)));
```

**Por que compensa:** o email carrega uma URL auto-expirante — sem precisar de "este link expirou; faça login para tentar de novo". Bônus: o serviço de email nunca vê os bytes do objeto.

### 3. URL de curta duração com invalidação de cache

```csharp
public Task<AxisResult<string>> GetDocumentUrlAsync(AxisEntityId documentId)
    => cache.GetOrCreateAsync(
        key:        $"document-url:{documentId}",
        factory:    () => storage.GetPresignedUrlAsync($"documents/{documentId}.pdf", TimeSpan.FromMinutes(10)),
        expiration: TimeSpan.FromMinutes(8));   // cache mais curto que o TTL da URL
```

**Por que compensa:** leituras repetidas não re-assinam. O TTL do cache fica deliberadamente abaixo do TTL da URL, então uma URL cacheada está sempre válida quando entregue.

---

## Veja também

- [O contrato `IAxisStorage`](iaxisstorage.md) — cada método
- [Upload / Download](upload-download.md) — quando você não pode delegar para o cliente
- [Adapter Cloudflare R2](cloudflare-r2.md) — como URLs são assinadas

---

↩ [Voltar à documentação do AxisStorage](README.md)
