# Upload / Download · `UploadAsync`, `DownloadAsync`

> The two workhorses. **Streams in, streams out** — the contract never buffers the whole object, so multi-gigabyte uploads and downloads pass through without OOM.

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

## When to use

Any time you need to move bytes between your app and the bucket. The methods take and return `Stream`, so you can wire them to files, HTTP request bodies, HTTP responses, anything that speaks streams.

## When *not* to use

| You want to… | Use instead |
|---|---|
| hand the client a URL it can fetch directly | [`GetPresignedUrlAsync`](presigned-urls.md) |
| check whether something is there before downloading | [`ExistsAsync`](iaxisstorage.md) |
| remove an object | [`DeleteAsync`](iaxisstorage.md) |

---

## Upload semantics

| Behaviour | Adapter |
|---|---|
| Overwrites an existing key silently | yes — there is no "if-not-exists" mode in the contract |
| Reads `content` once, sequentially | yes — pass a forward-only stream is fine |
| Sets `Content-Type` on the object | yes — required argument, no default |
| Returns `Ok()` on success | yes — no metadata back; query `ExistsAsync` if you need confirmation |

## Download semantics

| Behaviour | Adapter |
|---|---|
| Returns the body as a forward-only `Stream` | yes |
| **Caller owns the stream** | yes — wrap in `await using` to dispose |
| Returns `IsFailure` on missing key | yes (404 from R2/S3 surfaces as an exception → `AxisResult.TryAsync` converts) |

> **Sharp edge:** the returned `Stream` is owned by the caller. Always wrap the call in `await using` (or pass the stream into something that disposes it) or you will leak a connection.

---

## Real-world examples

### 1. Receiving an HTTP file upload

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

**Why it pays off:** the upload streams directly from the request body to the bucket — no `MemoryStream`, no temporary file, no kernel-level copy.

### 2. Serving a download to the response stream

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

**Why it pays off:** `Results.Stream` disposes the stream when the response finishes. The bytes flow from R2 to the client without a buffer in your process.

### 3. Server-to-server pipe

```csharp
public async Task<AxisResult> CopyTo(string sourceKey, string destKey)
{
    var download = await storage.DownloadAsync(sourceKey);
    if (download.IsFailure) return download.ToAxisResult();

    await using var src = download.Value;
    return await storage.UploadAsync(destKey, src, "application/octet-stream");
}
```

**Why it pays off:** a one-pass copy. The download stream feeds straight into the upload — your process never holds the object in memory or on disk.

---

## See also

- [The `IAxisStorage` contract](iaxisstorage.md) — every method
- [Presigned URLs](presigned-urls.md) — let the client skip the proxy
- [Cloudflare R2 adapter](cloudflare-r2.md) — how the bundled adapter implements these

---

↩ [Back to AxisStorage docs](README.md)
