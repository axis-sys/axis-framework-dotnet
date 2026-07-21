---
name: axis-storage
description: >
  Store and serve blobs (images, videos, documents, attachments) on Axis through `IAxisStorage` — one of the
  swappable infra ports. Use when implementing a file upload, a streaming download, a delete, an existence
  check, or a time-limited (presigned/servable) URL in a bounded context, and when choosing or writing a
  storage adapter (Cloudflare R2, Azure Blob, filesystem, or your own). This skill is a MAP: each row points
  to the canonical rule in `rules/` — open only the one the context asks for. It does NOT restate invariants
  nor carry code. It does NOT redefine the swappable-infra-port pattern in the abstract
  (→ axis-dotnet-architect), the return monad (→ axis-result), nor the ambient context/cancellation
  (→ axis-mediator).
---

# AxisStorage — rule map (blob upload/download and presigned URLs)

**Storage** is a driven port: a bounded context stores only metadata (an id/key/content-type) and calls
`IAxisStorage` for the bytes, never touching a vendor SDK directly. The port is five async methods, every
result an `AxisResult`, keyed by a string; the bucket/container, region and endpoint are the adapter's
business. The default adapter is Cloudflare R2 (S3-compatible); Azure Blob and a filesystem adapter ship in
the same package, and a custom adapter is five methods plus a DI registration. Three optional capabilities —
container administration, prefix listing, servable-URL resolution — live in separate interfaces an adapter
opts into. The package is 3-infra.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/3-infra/axis-storage/`; open **only** the rule the context
requires.

## Rule map

### Start here — route by intent ⭐

| Context / what you were about to write | Rule |
|---|---|
| The port itself — five methods, string key, every result an `AxisResult` | [storage-port-five-methods](../../rules/framework/3-infra/axis-storage/storage-port-five-methods.yaml) |
| About to wrap a storage call in `try/catch` — don't; it returns a rail | [storage-result-never-throws](../../rules/framework/3-infra/axis-storage/storage-result-never-throws.yaml) |
| Which store to pick, or writing your own adapter | [storage-custom-adapter-contract](../../rules/framework/3-infra/axis-storage/storage-custom-adapter-contract.yaml) |

### The five operations

| Context | Rule |
|---|---|
| Move bytes without buffering the whole object (streams in, streams out) | [storage-streaming-not-buffered](../../rules/framework/3-infra/axis-storage/storage-streaming-not-buffered.yaml) |
| Upload — overwrites silently, sets content type, returns no metadata | [storage-upload-overwrites](../../rules/framework/3-infra/axis-storage/storage-upload-overwrites.yaml) |
| Download — the returned stream is the caller's to dispose; missing key fails | [storage-download-caller-owns-stream](../../rules/framework/3-infra/axis-storage/storage-download-caller-owns-stream.yaml) |
| Delete — idempotent, no exists pre-check needed | [storage-delete-idempotent](../../rules/framework/3-infra/axis-storage/storage-delete-idempotent.yaml) |
| Exists — a typed `Ok(false)` on a missing key, not a 404 exception | [storage-exists-typed-boolean](../../rules/framework/3-infra/axis-storage/storage-exists-typed-boolean.yaml) |
| Presigned URL — GET-only, `now + expiration`, no existence check | [storage-presigned-url-get-only](../../rules/framework/3-infra/axis-storage/storage-presigned-url-get-only.yaml) |
| Cancellation — read from the ambient mediator, never a parameter | [storage-ambient-cancellation](../../rules/framework/3-infra/axis-storage/storage-ambient-cancellation.yaml) |

### Optional capabilities — container, lister, URL resolver

| Context | Rule |
|---|---|
| Why they are separate optional interfaces; inject only what you need | [storage-optional-capabilities-separate-interfaces](../../rules/framework/3-infra/axis-storage/storage-optional-capabilities-separate-interfaces.yaml) |
| Container admin — exists / ensure-exists / public-access probe | [storage-container-capability](../../rules/framework/3-infra/axis-storage/storage-container-capability.yaml) |
| Listing — every key under a prefix, paginated internally | [storage-lister-capability](../../rules/framework/3-infra/axis-storage/storage-lister-capability.yaml) |
| Servable URL decides and emits; the public-access probe only reports | [storage-servable-url-decides-vs-reports](../../rules/framework/3-infra/axis-storage/storage-servable-url-decides-vs-reports.yaml) |
| The `AxisStorageUrl` value object (Url, IsPublic, ExpiresAt) | [storage-url-value-object](../../rules/framework/3-infra/axis-storage/storage-url-value-object.yaml) |

### Registration, factory & the swap

| Context | Rule |
|---|---|
| In-box adapters are `internal sealed` — resolve via DI/factory, never `new` | [storage-adapter-internal-sealed](../../rules/framework/3-infra/axis-storage/storage-adapter-internal-sealed.yaml) |
| `AddAxis*Storage` — one Singleton exposed under every interface it implements | [storage-di-shared-singleton](../../rules/framework/3-infra/axis-storage/storage-di-shared-singleton.yaml) |
| Runtime destination (bucket per tenant) — the factory, cached per destination | [storage-factory-runtime-destination](../../rules/framework/3-infra/axis-storage/storage-factory-runtime-destination.yaml) |

### Cloudflare R2 adapter (`AxisStorage.CloudflareR2`)

| Context | Rule |
|---|---|
| Wiring — an `AmazonS3Client` for R2's S3-compatible endpoint | [storage-r2-s3-compatible-registration](../../rules/framework/3-infra/axis-storage/storage-r2-s3-compatible-registration.yaml) |
| Servable URL decided from `PublicUrl` config (no probe, no TTL) | [storage-r2-servable-url-from-config](../../rules/framework/3-infra/axis-storage/storage-r2-servable-url-from-config.yaml) |
| Gotcha — a null `S3Objects` page (MinIO) is coalesced to empty | [storage-r2-list-null-s3objects](../../rules/framework/3-infra/axis-storage/storage-r2-list-null-s3objects.yaml) |
| Gotcha — the public-access ACL probe may not reflect R2 reality | [storage-r2-public-access-acl-caveat](../../rules/framework/3-infra/axis-storage/storage-r2-public-access-acl-caveat.yaml) |

### Azure Blob adapter (`AxisStorage.AzureBlob`)

| Context | Rule |
|---|---|
| Credential (app identity) vs destination split — shared key (`AccountName`/`AccountKey`, Azurite/emulator) takes precedence over the AAD cascade | [storage-azure-credential-destination-split](../../rules/framework/3-infra/axis-storage/storage-azure-credential-destination-split.yaml) |
| SAS signing branches on the credential — user-delegation key on the AAD path; a shared-key client (emulator / key-based account) self-signs with the account key | [storage-azure-delegated-sas](../../rules/framework/3-infra/axis-storage/storage-azure-delegated-sas.yaml) |
| Public-access probe cached only on the servable-URL path (configurable TTL) | [storage-azure-public-access-cache-ttl](../../rules/framework/3-infra/axis-storage/storage-azure-public-access-cache-ttl.yaml) |
| Transient-failure retry with capped exponential backoff (inside the adapter) | [storage-azure-transient-retry](../../rules/framework/3-infra/axis-storage/storage-azure-transient-retry.yaml) |

### FileSystem adapter (`AxisStorage.FileSystem`)

| Context | Rule |
|---|---|
| Implements container + lister but not the URL resolver (`file://`, `Ok(true)`) | [storage-filesystem-capabilities-no-url-resolver](../../rules/framework/3-infra/axis-storage/storage-filesystem-capabilities-no-url-resolver.yaml) |
| Tolerates a null ambient mediator (runs in a background context) | [storage-filesystem-null-mediator-tolerated](../../rules/framework/3-infra/axis-storage/storage-filesystem-null-mediator-tolerated.yaml) |

## See also

- `axis-result` — the monad every storage method returns; adapters project SDK exceptions onto the railway with `AxisResult.TryAsync`.
- `axis-mediator` — the ambient `CancellationToken` the adapters read (never a parameter), and the accessor they resolve it from.
- `axis-use-case-cqrs` — the command/query that calls storage (upload in a command, servable URL in a query).
- `axis-saga` — `DeleteAsync` is the idempotent step a compensation runs to undo an upload.
- `axis-email` / `axis-cache` / `axis-bus` — the sibling swappable infra ports.
- `axis-dotnet-architect` — the hub; the swappable-infra-port pattern (`IAxis*` + `AxisResult` + `AddAxis*`) storage is one instance of.
