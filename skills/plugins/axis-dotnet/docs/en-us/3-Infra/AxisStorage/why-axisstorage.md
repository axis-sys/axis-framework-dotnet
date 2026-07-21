# Why AxisStorage? · comparison

> There are other ways to talk to object storage from .NET. This page tells you why AxisStorage is different — a direct comparison, no hand-waving.

---

## vs. `IAmazonS3` (directly)

The AWS SDK is the workhorse, and `AxisStorage.CloudflareR2` uses it internally. Calling it directly from handlers has three problems:

1. The SDK throws — every site needs `try/catch` or accepts crashes.
2. `BucketName`, `RegionEndpoint` and the credential plumbing leak into every caller.
3. Tests have to mock `IAmazonS3` with its huge surface, or run against a real bucket.

**AxisStorage** returns `AxisResult`, hides vendor concepts behind the adapter, and lets your tests use a local-disk implementation.

## vs. `BlobContainerClient` (Azure)

Same trade-offs as the AWS SDK: throwing API, vendor concepts everywhere, painful to mock. AxisStorage's five-method surface fits both backends — your application code does not change when you migrate.

## vs. `FileExtensions.WriteAllBytesAsync`

The "no abstraction" approach for local disk. Fine until you have to flip to cloud — then every site changes. `AxisStorage` keeps the local-disk option open (write a custom adapter) without leaking it into handlers.

## vs. a bespoke `IFileService`

DIY. Same shape as `IAxisStorage`, but you write the contract, the adapter, the streaming semantics and the failure handling yourself. `IAxisStorage` saves the cost — and inherits the railway story from `AxisResult`.

---

## The comparison

| Feature | AxisStorage | `IAmazonS3` direct | `BlobContainerClient` direct | Bespoke `IFileService` |
|---|:--:|:--:|:--:|:--:|
| Returns `AxisResult` | **Yes** | No | No | Maybe |
| Vendor concepts hidden in the adapter | **Yes** | No | No | Yes |
| Five-method surface, easy to mock | **Yes** | No | No | Yes |
| Swap R2 ↔ Azure Blob without app changes | **Yes** | No | No | Yes |
| Streaming up and down | **Yes** | Yes | Yes | Maybe |
| Presigned URLs as a first-class operation | **Yes** | Yes (verbose) | Yes (verbose) | Maybe |
| Bundled S3-compatible adapter | **Yes** | n/a | n/a | No |
| Implicit cancellation via `AxisMediator` | **Yes** | No | No | Maybe |

---

## See also

- [The `IAxisStorage` contract](iaxisstorage.md) — the surface
- [Cloudflare R2 adapter](cloudflare-r2.md) — the in-box implementation
- [Custom adapter](custom-adapter.md) — write one for your backend

---

↩ [Back to AxisStorage docs](README.md)
