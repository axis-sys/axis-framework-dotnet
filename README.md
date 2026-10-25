# Axis Framework

**An opinionated .NET framework for hexagonal, DDD and CQRS applications**, built on Railway-Oriented Programming:
every operation returns an `AxisResult`, every piece of infrastructure is a swappable port, and every use case is a
vertical slice exposed by a facade.

The framework ships as small NuGet packages, each one in its own repository. This repository is the **hub** that
ties them together:

- **The reference application** ([`scaffolds/`](scaffolds/README.md)): a small e-commerce built the Axis way, with
  five bounded contexts, one host and a four-project test pyramid (architecture, unit, integration and E2E). It
  consumes every Axis package from NuGet, exactly as your application would, so building and testing it proves the
  packages fit together.
- **The documentation site** ([`site/`](site/index.md)): the application guide, the articles of every package and
  the API reference generated from their XML docs, built with DocFX and published to GitHub Pages.
- **The application guide** ([`site/guide/`](site/guide/index.md)) and its counterpart for coding assistants,
  [`agents/axis-framework-dotnet.md`](agents/axis-framework-dotnet.md): how an Axis application is laid out and
  written, each convention linked to the code of the reference application that follows it.

## Packages

Install a package with `dotnet add package <id>`; pre-releases are published as `3.0.0-beta.N` (add `--prerelease`).
Each repository carries the package's code, tests, articles and its own usage agent.

### Foundations

| Repository | Packages | What it is |
|---|---|---|
| [`axis-result`](https://github.com/axis-sys/axis-result) | `AxisResult`, `AxisResult.Analyzers` | Railway-Oriented Programming for C#: the AxisResult monad, typed errors and their analyzers. |
| [`axis-testing`](https://github.com/axis-sys/axis-testing) | `Axis.Testing`, `Axis.Testing.XUnit` | AxisResult-aware test assertions for .NET: a framework-agnostic core and the xUnit v3 adapter. |
| [`axis-types`](https://github.com/axis-sys/axis-types) | `AxisTypes`, `AxisTypes.Analyzers` | Typed value objects for .NET: the `[ValueObject]` source generator, built-in value objects and their analyzers. |
| [`axis-dependency-injections`](https://github.com/axis-sys/axis-dependency-injections) | `AxisDependencyInjections`, `AxisDependencyInjections.Abstractions` | Axis-owned re-export of the Microsoft dependency injection packages: the abstractions and the container. |
| [`axis-mediator-contracts`](https://github.com/axis-sys/axis-mediator-contracts) | `AxisMediator.Contracts`, `AxisMediator.Contracts.Analyzers` | The CQRS contracts of Axis: requests, handlers, pipeline behaviors and the ambient mediator context, with analyzers. |

### Observability

| Repository | Packages | What it is |
|---|---|---|
| [`axis-logger`](https://github.com/axis-sys/axis-logger) | `AxisLogger` | Structured logging for Axis: the IAxisLogger port, ambient scope enrichment and the result-logging pipeline behavior. |
| [`axis-telemetry`](https://github.com/axis-sys/axis-telemetry) | `AxisTelemetry` | Tracing and metrics ports for Axis, the OpenTelemetry adapter and the telemetry pipeline behavior. |
| [`axis-telemetry-azure-monitor`](https://github.com/axis-sys/axis-telemetry-azure-monitor) | `AxisTelemetry.AzureMonitor` | Azure Monitor adapter of the Axis telemetry ports. |

### Application flow

| Repository | Packages | What it is |
|---|---|---|
| [`axis-validator`](https://github.com/axis-sys/axis-validator) | `AxisValidator` | Declarative input validation for Axis: validators that return AxisResult and the validation pipeline behavior. |
| [`axis-bus`](https://github.com/axis-sys/axis-bus) | `AxisBus`, `AxisBus.Analyzers` | The IAxisBus port: fire-and-forget domain events with parallel fan-out, and its analyzers. |
| [`axis-validator-fluent-validation`](https://github.com/axis-sys/axis-validator-fluent-validation) | `AxisValidator.FluentValidation` | FluentValidation adapter of the Axis validator. |
| [`axis-saga`](https://github.com/axis-sys/axis-saga) | `AxisSaga` | Saga orchestration for Axis: stages, compensation, leases, the resumer and the janitor. |
| [`axis-bus-repository`](https://github.com/axis-sys/axis-bus-repository) | `AxisBus.Repository` | Durable outbox for the Axis bus on the Axis repository: publish, dispatch, retry and backoff. |
| [`axis-mediator`](https://github.com/axis-sys/axis-mediator) | `AxisMediator` | The Axis mediator runtime: CQRS dispatch, the ambient context and the pipeline behaviors. |
| [`axis-validator-brazil`](https://github.com/axis-sys/axis-validator-brazil) | `AxisValidator.Brazil` | Brazilian document and format rules for the Axis validator. |
| [`axis-validator-usa`](https://github.com/axis-sys/axis-validator-usa) | `AxisValidator.Usa` | United States validation rules for the Axis validator. |
| [`axis-saga-mysql`](https://github.com/axis-sys/axis-saga-mysql) | `AxisSaga.MySql` | MySQL storage for Axis sagas. |
| [`axis-memory-bus`](https://github.com/axis-sys/axis-memory-bus) | `AxisMemoryBus` | In-memory adapter of the Axis bus port. |
| [`axis-bus-mysql`](https://github.com/axis-sys/axis-bus-mysql) | `AxisBus.MySql` | MySQL storage for the Axis bus outbox. |
| [`axis-saga-postgres`](https://github.com/axis-sys/axis-saga-postgres) | `AxisSaga.Postgres` | PostgreSQL storage for Axis sagas. |
| [`axis-bus-postgres`](https://github.com/axis-sys/axis-bus-postgres) | `AxisBus.Postgres` | PostgreSQL storage for the Axis bus outbox. |

### Infrastructure

| Repository | Packages | What it is |
|---|---|---|
| [`axis-cache`](https://github.com/axis-sys/axis-cache) | `AxisCache` | The IAxisCache port: a swappable cache contract that returns AxisResult and never throws. |
| [`axis-email`](https://github.com/axis-sys/axis-email) | `AxisEmail` | The IAxisEmailService port: a swappable e-mail contract that returns AxisResult and never throws. |
| [`axis-storage`](https://github.com/axis-sys/axis-storage) | `AxisStorage` | The IAxisStorage port: a swappable blob storage contract (upload, download, delete, exists, URLs) on AxisResult. |
| [`axis-ai`](https://github.com/axis-sys/axis-ai) | `AxisAI` | The AI chat completion port for Axis: messages, tools and errors as AxisResult values. |
| [`axis-security`](https://github.com/axis-sys/axis-security) | `AxisSecurity` | Axis security primitives: a purpose-bound cipher, HMAC signatures, blind indexes, PKCE and secure tokens, on AxisResult. |
| [`axis-pay`](https://github.com/axis-sys/axis-pay) | `AxisPay`, `AxisPay.InMemory` | The AxisPay payment port (charges, Pix transfers, notifications) and its in-memory adapter. |
| [`axis-repository`](https://github.com/axis-sys/axis-repository) | `AxisRepository` | Data access for Axis: IAxisDbRepository and IAxisUnitOfWork over ADO.NET, on AxisResult, plus the migration runner. |
| [`axis-http`](https://github.com/axis-sys/axis-http) | `AxisHttp` | Resilient HTTP for Axis: retry policy, deadlines, the safe fetcher and HTTP errors as AxisResult values. |
| [`axis-memory-cache`](https://github.com/axis-sys/axis-memory-cache) | `AxisMemoryCache` | In-memory adapter of the Axis cache port, over Microsoft.Extensions.Caching.Memory. |
| [`axis-storage-amazon-s3`](https://github.com/axis-sys/axis-storage-amazon-s3) | `AxisStorage.AmazonS3` | Amazon S3 adapter of the Axis storage port. |
| [`axis-storage-azure-blob`](https://github.com/axis-sys/axis-storage-azure-blob) | `AxisStorage.AzureBlob` | Azure Blob Storage adapter of the Axis storage port. |
| [`axis-storage-cloudflare-r2`](https://github.com/axis-sys/axis-storage-cloudflare-r2) | `AxisStorage.CloudflareR2` | Cloudflare R2 adapter of the Axis storage port. |
| [`axis-storage-filesystem`](https://github.com/axis-sys/axis-storage-filesystem) | `AxisStorage.FileSystem` | File system adapter of the Axis storage port. |
| [`axis-repository-postgres`](https://github.com/axis-sys/axis-repository-postgres) | `AxisRepository.Postgres` | PostgreSQL adapter of the Axis repository and unit of work, with the PostgreSQL migration dialect. |
| [`axis-repository-mysql`](https://github.com/axis-sys/axis-repository-mysql) | `AxisRepository.MySql` | MySQL adapter of the Axis repository and unit of work, with the MySQL migration dialect. |
| [`axis-jwt`](https://github.com/axis-sys/axis-jwt) | `AxisJwt` | JSON Web Tokens for Axis: HS256, ES256 and RS256 signing and validation, and a cached JWKS client. |
| [`axis-mcp-client`](https://github.com/axis-sys/axis-mcp-client) | `AxisMcp.Client` | A Model Context Protocol client for Axis over streamable HTTP, on AxisResult. |
| [`axis-ai-openai-compatible`](https://github.com/axis-sys/axis-ai-openai-compatible) | `AxisAI.OpenAiCompatible` | OpenAI-compatible adapter of the Axis AI chat port. |
| [`axis-pay-efi`](https://github.com/axis-sys/axis-pay-efi) | `AxisPay.Efi` | Efí (Pix) adapter of the AxisPay payment port. |
| [`axis-email-mimekit`](https://github.com/axis-sys/axis-email-mimekit) | `AxisEmail.MimeKit` | MimeKit and MailKit SMTP adapter of the Axis e-mail port. |
| [`axis-cache-repository`](https://github.com/axis-sys/axis-cache-repository) | `AxisCache.Repository` | Two-tier cache for Axis: L1 memory over a durable SQL L2, on the Axis repository. |
| [`axis-ai-agents`](https://github.com/axis-sys/axis-ai-agents) | `AxisAI.Agents` | An agent loop for the Axis AI chat port: tools, turn limits, compaction and hooks. |
| [`axis-cache-postgres`](https://github.com/axis-sys/axis-cache-postgres) | `AxisCache.Postgres` | PostgreSQL storage for the Axis two-tier cache. |
| [`axis-cache-mysql`](https://github.com/axis-sys/axis-cache-mysql) | `AxisCache.MySql` | MySQL storage for the Axis two-tier cache. |

### HTTP edge

| Repository | Packages | What it is |
|---|---|---|
| [`axis-openapi`](https://github.com/axis-sys/axis-openapi) | `AxisOpenApi` | OpenAPI for Axis web APIs: one document per version, operation and security conventions, and the Scalar UI. |
| [`axis-result-httpresponse`](https://github.com/axis-sys/axis-result-httpresponse) | `AxisResult.HttpResponse` | Renders AxisResult to ASP.NET Core responses: status mapping and RFC 7807 problem details. |

## Build and test

You need the .NET SDK pinned in [`global.json`](global.json) and Docker running (the integration and E2E tests
start Postgres and MySQL with Testcontainers). The Axis pre-releases restore from the organization's GitHub Packages
feed, so set `AXIS_NUGET_USER` (a GitHub user) and `AXIS_NUGET_TOKEN` (a token with `read:packages`) before the first
restore; [`NuGet.config`](NuGet.config) reads them from the environment.

```bash
dotnet restore AxisFramework.slnx
dotnet build AxisFramework.slnx -c Release --no-restore -warnaserror
dotnet test --solution AxisFramework.slnx -c Release --no-build
```

The version of every Axis package the reference application consumes is pinned in
[`Directory.Packages.props`](Directory.Packages.props).

## Documentation site

The site is generated from the `docs/` folder of each package repository listed in
[`site/packages.json`](site/packages.json), the guide in `site/guide/` and the XML docs of those packages. With the
package repositories cloned side by side in one folder:

```bash
node site/collect-docs.mjs <folder with the package repositories>
dotnet build site/ApiReference/ApiReference.csproj -c Release
dotnet tool restore
dotnet docfx site/docfx.json --serve
```

The [`docs`](.github/workflows/docs.yml) workflow does the same on every push to `main` and publishes the result to
GitHub Pages.

## Contributing

Issues and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). A change to a package belongs in that
package's repository; this one takes changes to the reference application, the guide and the site.

## License

[Apache-2.0](LICENSE)
