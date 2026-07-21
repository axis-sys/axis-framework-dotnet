# Presigned URLs · `GetPresignedUrlAsync`

> Mint a **time-limited URL** the client can use to fetch the object directly from the bucket. No proxying through your API, no egress on your application servers, and one less stream for your process to babysit.

```csharp
var result = await storage.GetPresignedUrlAsync(
    key:        $"exports/{exportId}.csv",
    expiration: TimeSpan.FromMinutes(15));

if (result.IsSuccess)
    return new DownloadResponse { Url = result.Value };
```

---

## When to use

- The client (browser, mobile app) can talk to the bucket directly.
- The object is large enough that proxying through your app would dominate your egress bill.
- You want to expire access automatically — handing out a URL that stops working in 15 minutes is the simplest form of authorisation.

## When *not* to use

| You want to… | Use instead |
|---|---|
| stream bytes through your app (auth, transformation, watermarking) | [`DownloadAsync`](upload-download.md) |
| keep a permanent public URL | configure a bucket-level public read policy |
| upload from the client | a *presigned **PUT*** URL — the in-box adapter implements GET only |

---

## Behaviour

| Aspect | Behaviour |
|---|---|
| HTTP verb | `GET` (the in-box adapter calls `GetPreSignedURLAsync` with `Verb = HttpVerb.GET`) |
| Expiration | `now + expiration` — passed as `DateTime.UtcNow.Add(expiration)` |
| Bucket | the one configured in `CloudflareR2Settings.BucketName` |
| Object existence | **not** checked — a URL is minted whether or not the key exists; the **client** sees a 404 when it tries to fetch a missing key |
| Cancellation | honoured (the SDK call accepts the ambient token) |

> **Sharp edge:** there is no `Verb`/`Method` parameter in the contract. The bundled adapter mints **GET** URLs only. For uploads, write a small extension on your adapter or expose a richer abstraction.

---

## Real-world examples

### 1. Avatar download via signed URL

```csharp
public Task<AxisResult<GetAvatarUrlResponse>> HandleAsync(GetAvatarUrlQuery query)
    => storage.GetPresignedUrlAsync(
            key:        $"avatars/{query.PersonId}.png",
            expiration: TimeSpan.FromHours(1))
        .MapAsync(url => new GetAvatarUrlResponse { Url = url });
```

**Why it pays off:** the client gets a URL it caches for an hour. Your API is hit once per renewal, not once per image. The browser pulls from R2 over a CDN, not from your origin.

### 2. Email a signed export link

```csharp
public Task<AxisResult> NotifyExportReadyAsync(ExportReadyEvent @event)
    => storage.GetPresignedUrlAsync($"exports/{@event.ExportId}.zip", TimeSpan.FromDays(1))
        .ThenAsync(url => email.SendAsync(new ExportReadyMessage(@event.UserEmail, url)));
```

**Why it pays off:** the email carries a self-expiring URL — no need for "this link was valid; sign in to retry". Bonus: the email service never sees the object's bytes.

### 3. Short-lived URL with cache invalidation

```csharp
public Task<AxisResult<string>> GetDocumentUrlAsync(AxisEntityId documentId)
    => cache.GetOrCreateAsync(
        key:        $"document-url:{documentId}",
        factory:    () => storage.GetPresignedUrlAsync($"documents/{documentId}.pdf", TimeSpan.FromMinutes(10)),
        expiration: TimeSpan.FromMinutes(8));   // cache shorter than the URL's TTL
```

**Why it pays off:** repeated reads do not re-sign. The cache TTL deliberately undershoots the URL's TTL, so a cached URL is always still valid when handed out.

---

## See also

- [The `IAxisStorage` contract](iaxisstorage.md) — every method
- [Upload / Download](upload-download.md) — when you cannot delegate to the client
- [Cloudflare R2 adapter](cloudflare-r2.md) — how URLs are signed

---

↩ [Back to AxisStorage docs](README.md)
