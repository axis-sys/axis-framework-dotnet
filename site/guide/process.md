# Process

This page covers how an Axis application is worked on, beyond the shape of its code: what code may say about
itself, how configuration, database providers and migrations are handled, how documentation is kept, what to check
before a commit, how to split work across bounded contexts, and what the delivered history may contain. The
reference application under
[`scaffolds/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/README.md) applies each of
these practices.

| Section | In one line |
|---|---|
| [Comments](#comments) | no inline comments, anywhere |
| [XML docs](#xml-docs) | only on what renders in the OpenAPI document |
| [Layered configuration](#layered-configuration) | committed defaults, local overrides, user-secrets, environment variables |
| [Provider switches](#provider-switches) | the database engine is configuration, read only by the composition root |
| [No I/O at startup](#no-io-at-startup) | the host only composes; migrations run through a dedicated command |
| [Developer CLI](#developer-cli) | a standalone tool that drives the API over HTTP |
| [Documentation](#documentation) | a lean README, the OpenAPI UI as the hub, docs next to the code |
| [Review before a commit](#review-before-a-commit) | deterministic gates first, judgment last |
| [Parallel work across bounded contexts](#parallel-work-across-bounded-contexts) | only across contexts with no pending dependency |
| [Clean delivery](#clean-delivery) | no trace of the tools used to write the code |

---

## Comments

Code documents itself, so inline `//` comments are banned from the whole codebase. The reference application has
none.

- No `//` comment anywhere. That means no narration of what the next line does, no rationale, and no footgun
  warning either. Even a critical footgun note belongs in the project's documentation, where it lasts, and not in
  a line of code.
- Tests and DI registrations follow the same rule. A test's purpose lives in its name and in the acceptance
  specification it proves. The reason behind a registration that encodes a decision lives in the convention it
  fulfills or in the project's README.
- A comment never explains a change to a reviewer. That explanation goes in the pull request.
- No banner comments, no `#region` markers and no commented-out code.
- XML docs (`///`) are kept only where they render in the OpenAPI/reference documentation. See
  [XML docs](#xml-docs).
- Original XML doc text lives at the root of an inheritance chain. Implementations and overrides carry
  `<inheritdoc/>`, which becomes mandatory as soon as the root is documented.
- Nobody adds a new comment to delivered code without first agreeing it with the people who own the code.

To check a change quickly before you commit:

```bash
git grep -n -E '^[[:space:]]*(//([^/]|$)|#region)' -- '*.cs'
```

---

## XML docs

`///` docs exist only because they feed the OpenAPI document, so they are written only on what reaches that
document. What counts is whether a type shows up in the document, not which project it lives in.

| Where | XML docs | In the reference application |
|---|---|---|
| Controllers and their actions | `<summary>`, `<remarks>`, `<param>`, `<response>` | [`ProductsV1Controller`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Catalog/ProductsV1Controller.cs) |
| Request bodies and responses a controller exposes, and their sub-DTOs | a type `<summary>` that describes behavior, a `<summary>` on each property, and an `<example>` where a sample value helps the docs UI | [`CheckoutProductCommand`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/CheckoutProduct/CheckoutProductCommand.cs), [`GetValidationStatusResponse`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/EmailValidation/v1/GetValidationStatus/GetValidationStatusResponse.cs) |
| A query or command that the controller builds itself from route values | none. The action's `<param>` documents the route value | [`GetProductQuery`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/GetProduct/GetProductQuery.cs) |
| A property filled from the route and marked `[JsonIgnore]` | none, because it never appears in the schema | `CheckoutProductCommand.ProductId` |
| A driving DTO reached only through a facade, such as a cross-context command or the grants query read by the permission filter | none | [`EnsureCustomerCommand`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Customers/v1/EnsureCustomer/EnsureCustomerCommand.cs), [`GetGrantsQuery`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Authorization/v1/GetGrants/GetGrantsQuery.cs) |
| Facade interfaces, driven ports, domain properties interfaces, kernel types | none | [`IProductsFacade`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/IProductsFacade.cs) |
| Integration events and saga payloads | none | [`ProductCheckedOutEvent`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Events/v1/ProductCheckedOutEvent.cs), [`ValidateEmailPayload`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/EmailValidation/v1/Sagas/ValidateEmail/ValidateEmailPayload.cs) |
| Tests, including architecture and boundary tests | none. What a test checks is in its name | |

Since the docs live on the DTOs, the type summary is where to explain behavior that a reader would otherwise want
a comment for:

```csharp
/// <summary>
/// Reserves stock of a product for a cart of the authenticated customer; the order is submitted afterwards.
/// Fails with a business rule when the requested quantity exceeds the available stock.
/// </summary>
public sealed record CheckoutProductCommand : IAxisCommand<CheckoutProductResponse>
{
    /// <summary>Id of the cart the product is checked out into.</summary>
    /// <example>0192f3a1-fb9c-7d61-9e5f-3c1051b60001</example>
    public string? CartId { get; init; }

    [JsonIgnore]
    public string? ProductId { get; init; }

    /// <summary>Number of units to reserve. Must not exceed the product's available stock.</summary>
    /// <example>2</example>
    public int? Quantity { get; init; }
}
```

The host passes [`AxisOpenApi`](https://github.com/axis-sys/axis-openapi) the assemblies whose XML docs feed the
document: its own assembly, which holds the controllers, and the driving contracts.

```csharp
builder.Services
    .AddAxisUrlVersioning()
    .AddAxisOpenApi(ECommerceOpenApi.Configure, typeof(Program).Assembly, typeof(IProductsFacade).Assembly);
```

Most public types are left undocumented on purpose. A project that generates its documentation file should
therefore turn off the missing-XML-comment warning (`CS1591`); otherwise `-warnaserror` turns the policy into a
build failure. [Edge](edge.md) covers the OpenAPI wiring itself.

---

## Layered configuration

Configuration comes in layers, so credentials stay out of source control and the keys a developer needs are
still easy to find.

| Layer | Holds | Committed | In the reference application |
|---|---|---|---|
| `appsettings.json` | a default for every key, an empty string in each secret slot, and a `_doc` key on each complex block | yes | [`appsettings.json`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/appsettings.json) |
| `appsettings.Development.json` | values specific to the developer's machine (localhost connection strings, allowed origins, longer local token lifetimes) and the secrets of the local docker stack, nothing else | yes | [`appsettings.Development.json`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/appsettings.Development.json) |
| `secrets.template.json` | the keys a developer has to supply separately, with empty values | yes | [`secrets.template.json`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/secrets.template.json) |
| user-secrets | every other real credential, on the developer's machine | never | `UserSecretsId` in [`Scaffolds.ECommerce.Host.csproj`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Scaffolds.ECommerce.Host.csproj) |
| environment variables | `Section__Key`, with a double underscore. Inside containers they override everything else | n/a | `ConnectionStrings__ECommercePostgres`, `Database__Provider` |

- A development-only secret of the local docker stack, such as the password of the docker-compose database, may
  be committed in `appsettings.Development.json`, because it unlocks no shared environment. This allowance is
  narrow on purpose. A value that only unlocks a container on the developer's own machine is configuration of that
  machine. Anything that reaches a shared database, an identity provider or a third-party API is a real secret,
  even in development.
- No other secret is ever committed: signing keys, client secrets, credentials of any shared environment. The
  defaults ship each one as an empty string, and the template lists its key.
- Those secrets come from user-secrets on a developer's machine and are mounted read-only into containers. They
  are never baked into an image as literal values. The host keeps the template out of its build and publish
  output:
  `<Content Update="secrets.template.json" CopyToOutputDirectory="Never" CopyToPublishDirectory="Never" />`.
- Choosing a provider or a store is configuration. Only the composition root reads those keys, and each key has a
  documented default (see [Provider switches](#provider-switches)).
- Connection-string names are fixed contracts (`ECommercePostgres`, `ECommerceMySql`). A derived store builds its
  connection from the one it reuses.
- Operational settings are configuration, not code, so each environment can tune them without a rebuild. That
  covers pool sizes, batch windows, poll intervals and background toggles, such as `Database:MaxConnections`,
  `Saga:MaxPoolSize`, `Bus:PollInterval` and `Cache:SweepInterval`.
- A complex settings block carries an inline documentation key that explains its sizing or what its credentials
  mean.
- A credential block left empty on purpose can mean the application uses ambient or managed credentials. That is a
  deliberate configuration with no secret in it, not a missing value.

```json
"Token": {
  "_doc": "SigningKey is a secret and ships empty: supply it through user-secrets locally (keys listed in secrets.template.json) and through the environment in containers.",
  "Issuer": "https://scaffolds-ecommerce.dev",
  "Audience": "scaffolds-ecommerce-api",
  "AccessTokenLifetimeSeconds": 900,
  "SigningKey": ""
}
```

To set it locally, run this from the repository root:

```bash
dotnet user-secrets set "ECommerce:Auth:Token:SigningKey" "<at least 32 characters>" --project scaffolds/Host/Scaffolds.ECommerce.Host
```

These layers are a security control as much as a convenience: the committed layers never hold a live credential
for a shared environment. [`AuthenticationCompositionTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/AuthenticationCompositionTests.cs)
checks that the host refuses to start when a required setting is missing.

---

## Provider switches

When an application supports more than one database engine, the engine is chosen in configuration, and only the
composition root reads that choice.

| Switch | Selects | Default |
|---|---|---|
| `Database:Provider` | the business repositories. The outbox and the durable cache use the same engine | `Postgres` |
| `Saga:Provider` | the saga store, chosen on its own | `Postgres` |

- If a switch is unset, the default provider applies. The value is compared case-insensitively, and an unknown
  value stops the composition with an error.
- Every store that is provisioned on its own gets its own switch with the same rules, so stores can run on
  different engines.
- Each provider's DI extension exposes its connection-string name as a constant (`ConnectionStringName`). A derived
  store reuses the connection of the provider it was given: the saga store takes its engine's connection string
  and opens its own pool.
- Pool sizes are set per store in configuration and applied in the composition root, so stores that are
  provisioned separately keep separate pools. With a shared pool, a burst of work on one store can use up every
  connection and starve the other. The reference application also refuses to start when the pools that share one
  server add up to more than `Database:MaxConnections` (see [Persistence](persistence.md)).
- Each switch maps to the matching `Add*` registration. No repository, handler or controller ever branches on the
  provider, which keeps the rest of the code independent of the engine and lets it return `AxisResult` the same way
  everywhere.
- An application that supports a single engine needs no switch. The engine names here are examples; which engines
  to support is up to the application.

```csharp
private const DatabaseProvider DefaultProvider = DatabaseProvider.Postgres;

public static DatabaseProvider Provider(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
        return DefaultProvider;

    return Enum.GetValues<DatabaseProvider>()
        .Where(provider => string.Equals(provider.ToString(), value, StringComparison.OrdinalIgnoreCase))
        .Select(provider => (DatabaseProvider?)provider)
        .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"Unknown {key} '{value}'; expected one of {string.Join(", ", Enum.GetNames<DatabaseProvider>())}.");
}
```

```csharp
return provider switch
{
    DatabaseProvider.Postgres => services.AddECommercePostgres(configuration).AddAxisBusPostgres(bus),
    DatabaseProvider.MySql => services.AddECommerceMySql(configuration).AddAxisBusMySql(bus),
    _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
};
```

The switches select the provider packages:
[`AxisRepository.Postgres`](https://github.com/axis-sys/axis-repository-postgres) /
[`AxisRepository.MySql`](https://github.com/axis-sys/axis-repository-mysql),
[`AxisBus.Postgres`](https://github.com/axis-sys/axis-bus-postgres) /
[`AxisBus.MySql`](https://github.com/axis-sys/axis-bus-mysql),
[`AxisCache.Postgres`](https://github.com/axis-sys/axis-cache-postgres) /
[`AxisCache.MySql`](https://github.com/axis-sys/axis-cache-mysql),
[`AxisSaga.Postgres`](https://github.com/axis-sys/axis-saga-postgres) /
[`AxisSaga.MySql`](https://github.com/axis-sys/axis-saga-mysql).

See:
[`PersistenceConfiguration.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/PersistenceConfiguration.cs) ·
[`PersistenceKeys.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/PersistenceKeys.cs) ·
[`Persistence/DependencyInjection.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/DependencyInjection.cs) ·
[`ConnectionPools.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/ConnectionPools.cs) ·
[Postgres `DependencyInjection.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Postgres/DependencyInjection.cs) ·
[MySql `DependencyInjection.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.MySql/DependencyInjection.cs) ·
[`PersistenceCompositionTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/PersistenceCompositionTests.cs),
which tests the default, the case-insensitive match and the refusals.

---

## No I/O at startup

The composition root only composes. It never opens a connection, runs a migration or touches storage while the
host is being built or shut down. Once the host is built, the web path only wires up the pipeline:

```csharp
var app = builder.Build();

if (MigrateCommand.IsRequested(args))
    return await MigrateCommand.RunAsync(app);

app.UseAuthentication();
app.UseAxisAmbientIdentity(AuthClaimTypes.CustomerId);
app.UseAuthorization();
app.MapControllers();
app.MapAxisOpenApi(configure: null, AuthSchemes.ECommerce);

await app.RunAsync();
return 0;
```

- The composition root performs no I/O, awaited or blocking. Reading configuration is not I/O, and neither is
  stopping early on a missing or invalid setting.
- A schema migration is an explicit, idempotent operation that you run on demand through the migrator port
  ([`IDatabaseMigratorPort`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Admin/IDatabaseMigratorPort.cs)).
  The web host never migrates on startup. A migration can be started in two ways:
  - The host's **`migrate` verb**, a separate run mode of the same binary
    ([`MigrateCommand`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/MigrateCommand.cs)).
    Once the host is built, it runs the migrator instead of the web host and exits with `0` when every schema is up
    to date, or `1` when a migration fails. Run it on every deploy and on the first run against an empty database.
  - An **admin-only endpoint** routed to the migration use case, `POST /api/v1/database-migrations`
    ([`DatabaseMigrationsV1Controller`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Admin/DatabaseMigrationsV1Controller.cs)).
    It re-runs the same migration whenever an administrator asks.
- Long-running background work is switched on and off by configuration: the saga resumer and janitor, the outbox
  dispatcher and the cache sweep (`Saga:ResumerEnabled`, `Saga:JanitorEnabled`, `Bus:DispatcherEnabled`,
  `Cache:SweepEnabled`). Unset toggles keep the framework's defaults, so a test or a constrained environment can turn
  this work off without changing code.
- A test host controls startup only through configuration. The E2E
  [`ECommerceWebAppFactory`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Factories/ECommerceWebAppFactory.cs)
  passes the connection string and the provider switches as environment variables and the auth settings as
  in-memory settings, then migrates through the port once the host is built.

From the repository root:

```bash
dotnet run --project scaffolds/Host/Scaffolds.ECommerce.Host -- migrate
dotnet run --project scaffolds/Host/Scaffolds.ECommerce.Host
```

This avoids a common trap: a migration that runs before the host is built ignores the test host's configuration
overrides. The fix is to remove startup I/O altogether, not to move it earlier or later. The convention limits
what the composition root does, not which host model it uses. The migrations themselves run on
[`AxisRepository`](https://github.com/axis-sys/axis-repository)'s `IAxisMigrationRunner` (see
[Persistence](persistence.md)).

See:
[`Program.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Program.cs) ·
[`DatabaseMigratorAdapter.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/DatabaseMigratorAdapter.cs) ·
[`MigrateCommandTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/Admin/MigrateCommandTests.cs),
which runs the verb twice on an empty database and checks the failure exit code ·
[`MigrateDatabaseTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/Admin/MigrateDatabaseTests.cs)

---

## Developer CLI

If the application has a developer CLI, it is a pure HTTP client of the Web API. The reference application doesn't
ship one. When you build one, give it this shape:

- It sits outside the product source tree. It opts out of central package management and of the shared build
  props, and it pins its dependency versions locally on purpose. It may even pin preview or locally fixed versions
  that product code is not allowed to use. Keeping it outside the tree also keeps it out of the product build and
  image, so it can never leak into what ships.
- It references no product project. Everything it does goes through HTTP, using the application's own auth flow.
- It is packaged as a standalone command-line tool, and its command name matches the tool. The project can keep
  the `{App}.` prefix and set its `AssemblyName` to the published name (see
  [Project naming](style.md#project-naming)).
- It follows the same hexagonal layout as the application, on a small scale.
- A single declarative route catalog is the source of truth for every route it can call, with the group, verb,
  method, path template, auth mode, tenancy requirement and a sample body. Both the generic commands and the
  generated ones read from it.
- Route paths carry the same version prefix as the controllers (`/api/v1/...`). Adding an endpoint to the API means
  adding its entry to the catalog. Because the catalog is maintained by hand, comparing it with the OpenAPI
  document from time to time is a cheap way to catch drift.
- It has its own README, linked from the root README, that covers installation, commands, configuration and
  troubleshooting. It is tested end to end against the in-memory host.

---

## Documentation

Documentation is a map that points to the single source of each concern, not a second copy of it. Each concern is
written down once, and every other page links to it.

| Concern | Single source |
|---|---|
| The HTTP contract, the domain vocabulary, the bounded contexts, authentication | the OpenAPI reference UI, fed by the API description and the `///` docs |
| The conventions | this guide |
| Architecture decisions | the decision records |
| Code | the compiled, tested reference application. Excerpts are copied from it, never written by hand |

**The README** says only how to run, test and find your way around the repository. It holds a short pitch, a
pointer to the docs UI as the full picture, the repository layout, how to run, how to test, a pointer to any
tooling, and a documentation map. Domain, architecture and auth prose is written once, in the OpenAPI
description, and the README never explains it again. The reference application's
[README](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/README.md) follows this shape.

**The docs UI is the hub.** In Development, or anywhere `AxisOpenApi:Publish` is `true`, the host serves the
reference UI at `/scalar` and the document at `/openapi/v1.json`. The description in
[`ECommerceOpenApi`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/OpenApi/ECommerceOpenApi.cs)
covers the vocabulary, the API shape, authentication, the bounded contexts, the long-running operations and the
tooling. Every endpoint documents its responses, and an E2E test
([`OpenApiDocumentTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Documentation/OpenApiDocumentTests.cs))
keeps it that way. The convention does not require a particular renderer. [Edge](edge.md) has the details.

- A `docs/` folder stays small and purposeful. It holds deep-dives and walk-throughs that cannot live in an API
  reference, and it links out instead of repeating what the reference already says. Runbooks, which are often
  written after an incident or to deal with a piece of debt, and the onboarding sequence (the reading order and the
  documentation map) follow the same rules.
- Tool docs live next to the tool, and the README's documentation map links to them.
- A translation mirrors the canonical tree. It is a copy in another language, never a separate source.
- Docs live next to the code, are versioned with it, and change in the **same pull request** as the behavior they
  describe. That includes the README layout block when the project structure changes, an endpoint's `///` prose
  and each runbook step. One lag is tolerated: a generated coverage or route-map doc maintained alongside the tests
  may trail the code by a single pull request.

Before you merge a documentation change:

- [ ] Nothing that already has a single source is repeated; it is linked instead.
- [ ] Every code excerpt comes from compiled, tested code.
- [ ] API prose lives as `///` on the HTTP surface, not on a separate page.
- [ ] The README holds only the allowed blocks, and its layout block changed along with any change to the
  project structure.
- [ ] The doc ships in the same pull request as the behavior it documents.

---

## Review before a commit

Review the **diff**, not the whole repository, and don't check by hand what a machine already checks. Run the
deterministic gates first and apply judgment last.

### 1. Deterministic gates

Run these from the repository root, and fix every failure before you move on:

```bash
dotnet build AxisFramework.slnx -c Release -warnaserror
dotnet test --solution AxisFramework.slnx -c Release
```

The integration and E2E tests start their databases with Testcontainers, so Docker must be running.

The build runs the analyzers, and `-warnaserror` turns their warnings into errors. Anything they report is
already covered, so don't repeat it in the review:

| Diagnostics | Shipped with | What they catch |
|---|---|---|
| `AXIS0001`–`AXIS0008` | [`AxisResult`](https://github.com/axis-sys/axis-result/blob/main/docs/analyzers.md) | railway mistakes: reading `.Value` directly, branching on `IsSuccess`, `try/catch` inside a method that returns a result, and more |
| `AXIS0200`–`AXIS0201` | `AxisTypes.Analyzers` ([axis-types](https://github.com/axis-sys/axis-types)) | a `[ValueObject]` struct that is not `readonly`; `null` converted through the implicit conversion from string |
| `AXIS0300` | `AxisBus.Analyzers` ([axis-bus](https://github.com/axis-sys/axis-bus)) | an event published after the unit of work commits |
| `AXIS0400`–`AXIS0403` | `AxisMediator.Contracts.Analyzers` ([axis-mediator-contracts](https://github.com/axis-sys/axis-mediator-contracts)) | a handler that takes a `CancellationToken` parameter, a CQRS dispatch from inside a handler, an injected context accessor, a string-literal pipeline key |
| `AXIS0600`–`AXIS0608` | `Axis.Conventions.Analyzers` | handlers and validators that are not `internal sealed`, controllers that are not `sealed`, test method naming, domain properties records that are not `internal sealed`, route-filled properties without `[JsonIgnore]`, entity records with parameters crammed onto shared lines, a second public `DependencyInjection` class, an `AxisError` rebuilt from an errors-class `const` |

The reference application references `Axis.Conventions.Analyzers` once for every project, in
[`Directory.Build.props`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Directory.Build.props).

### 2. Judgment on the diff

Next, review the diff for what no analyzer can decide.

**Topology and boundaries.** The architecture tests
([`HexagonalBoundaryTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/HexagonalBoundaryTests.cs),
[`FacadeBoundaryTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/FacadeBoundaryTests.cs))
enforce these deterministically, so confirm that they cover the diff and pass. If an application has no such tests,
check by hand that:

- dependencies point inward: no core or contracts project references an adapter, only the host composes,
  `Contracts/` sits next to `Core/` rather than inside it, and facade implementations live in
  `{App}.Adapters.Driving.Facade`, never in the application ([Architecture](architecture.md));
- types are `public` only when they cross a project boundary and are an interface or a dumb record, and
  implementations are `internal sealed` ([Code style](style.md#access-modifiers));
- no bounded context injects another one's write port, repository or service, and calls between contexts go
  through a facade, `IAxisBus` or a saga;
- only the composition root picks an adapter (`Add*` / `AddAxis*`), and every call site depends on an
  abstraction.

**Forbidden packages.** Read the `PackageReference` items, not the code: no `FluentAssertions`, and Moq is the only
mocking library ([Testing](testing.md)).

**Folder and namespace layout:**

- every new file sits in `{BC}/{Aggregate}/{Feature}` in every layer, with one subfolder per feature;
- the namespace mirrors the folder, including the version segment `.v1`;
- commands, queries and responses live in `{App}.Contracts.Driving` as primitive DTOs, never in the application;
- `///` appears only on the HTTP surface, and `//` appears nowhere ([Comments](#comments), [XML docs](#xml-docs)).

**Semantic invariants:**

| Check | Where |
|---|---|
| A repository builds on `IAxisDbRepository` and its dialect seam rather than a base class, binds named parameters and selects `{Entities}Columns.All`. SQL is parameterized and never built by concatenating or interpolating input | [Persistence](persistence.md) |
| A handler casts its value objects at the top, then runs a single railway chain with no logic outside it, and never re-checks authorization that the edge already enforced | [Architecture](architecture.md) |
| A swappable infrastructure port (`IAxis*`) returns `AxisResult`, and `try/catch` appears only in its adapter. Cache-aside composes with `AxisResult`, and a mutation invalidates the entry | [Architecture](architecture.md) |
| A saga is declared with `AxisSagaDefinitions.Define<T>`, its resumer is hosted by the framework and switched by configuration, and compensation runs in reverse order | [Architecture](architecture.md) |
| A controller injects only facades and ends each action with `HttpContext.SendAsync` | [Edge](edge.md) |
| The aggregate level (N0, N1 or N2) is right, and the first business rule promotes the aggregate to N2 in the same pull request | [Domain](domain.md) |
| A unit test drives the facade through a `ServiceProvider` and mocks only driven ports. A new or changed controller has E2E coverage: the happy path, 401 and 403 | [Testing](testing.md) |

If the application keeps architecture diagrams and decision records, check in the same pass that a new bounded
context has its diagram, a structural decision has its record, and the diagrams still match the code.

### 3. Verdict

Report findings by severity. Each one names the convention and the `path:line` in the diff:

| Severity | Meaning | Effect |
|---|---|---|
| critical | a mandatory convention is broken | blocks the commit until it is fixed |
| warning | a recommended convention is broken, or there is a strong doubt | recorded; the author decides |
| info | optional or purely a matter of style | noted |

If nothing survives the review, the verdict is "0 findings" and the commit goes ahead.

Over time, move checks from judgment into the gates. Architecture tests make the topology checks deterministic. A
build target that fails on a banned package replaces the manual package check. Anything that becomes decidable
within a single compilation becomes an analyzer. Layout and intent stay with the reviewer.

---

## Parallel work across bounded contexts

When a plan spans several bounded contexts and the work is split between people or automated workers running in
parallel, run contexts in parallel only when nothing is pending between them.

1. Draw a dependency graph whose edges are **consumption** relationships. An edge exists when a context calls
   another context's facade, when a saga stage reads another context's reader port, or when a context subscribes
   to another context's bus event.
2. Put two contexts in parallel only if no edge joins them, in either direction.
3. A context that has an edge onto another one can start as soon as the **specific contract it consumes** is done
   in the producer: the facade member, the saga stage or the event definition. It does not wait for the whole
   producing context.
4. Write the graph and the resulting waves explicitly into the implementation plan.

- A context with no edge onto any other context in scope starts right away, whatever the others' progress. That
  holds even if another context will consume it later.
- Read-only use of the shared kernel (value objects, shared constants) does not count as an edge.

The reason: splitting the work by context without a graph lets a consumer start before the facade, stage or event
it depends on exists. [Architecture](architecture.md) describes the three channels themselves.

The graph for the reference application:

| Consumer | Consumes | Producer | Channel |
|---|---|---|---|
| Auth | `EnsureCustomerAsync`, in [`RequestTokenHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/UseCases/RequestToken/v1/RequestTokenHandler.cs) | Customers | facade |
| Authorization | `GetCustomerAsync`, in [`GrantsCacheService`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Services/GrantsCache/GrantsCacheService.cs) and `UpdateGrantsHandler` | Customers | facade |
| EmailValidation | `GetCustomerAsync`, plus `MarkEmailValidatedAsync` and `RevertEmailValidatedAsync` from its saga stages ([`MarkEmailValidatedStageHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/Handlers/MarkEmailValidatedStageHandler.cs)) | Customers | facade |
| Catalog | nothing from other contexts. It consumes its own `ProductCheckedOutEvent` ([`ProductCheckedOutEventHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/CartItems/Events/ProductCheckedOutEventHandler.cs)) | n/a | n/a |

Customers and Catalog start together. Auth, Authorization and EmailValidation then run in parallel with each other,
and each one starts as soon as the Customers facade members it consumes are done. The edge of the application
counts too. The host's permission filter
([`PermissionAuthorizationFilter`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Authorization/PermissionAuthorizationFilter.cs))
reads Authorization's `GetGrants` query, so an endpoint guarded by a permission is only complete once that query
exists.

---

## Clean delivery

Everything delivered carries no trace of the tools used to write it. That means the history and the content that
a client or a downstream consumer can see.

- Commit messages carry no trailer or footer that credits a writing tool, and no tool-signature emoji.
- Commit messages are meaningful and follow the conventional `type(scope): summary` style. Placeholders such as
  `wip` or `bkp` are not good enough to deliver.
- Branch names describe the task, the way a person would name it. Tool-style prefixes (`worktree-*`, `agent/*` and
  the like) never reach the shared history. A local-only worktree is fine, as long as its branch is never merged
  under such a name.
- No code, error message, seed value or doc mentions the tools used to produce it.
- No tool-specific file is committed. Local tool state stays untracked through one generic ignore block that
  covers many vendors at once, so the ignore file itself does not reveal which tool was used.
