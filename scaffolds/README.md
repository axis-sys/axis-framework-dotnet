# Scaffolds — the ECommerce reference application

A small e-commerce built the Axis way: one host composes the Auth, Authorization, Catalog, Customers and
EmailValidation bounded contexts over Postgres or MySQL. It consumes every Axis package from NuGet, exactly as
any application does, and it is the compiled, tested ground truth of the [application guide](../site/guide/index.md).

**Domain language: English.** Every domain term — identifiers, error codes, bus topics, seed values — is English.

**The full picture is the API reference.** Run the host in Development and open `/scalar`: its description
carries the vocabulary of the domain, the bounded contexts, authentication, permissions and the saga runs,
and every endpoint documents its responses.

## Layout

```
AxisFramework.slnx    (repository root)
scaffolds/
├─ Core/       Scaffolds.ECommerce.SharedKernel · .Domain · .Application
├─ Contracts/  Scaffolds.ECommerce.Contracts.Driving · .Contracts.Driven
├─ Adapters/   Scaffolds.ECommerce.Adapters.Driving.Facade · .Adapters.Driven.{Repository, Postgres, MySql, Jwt, AuthProviders, InMemory}
├─ Host/       Scaffolds.ECommerce.Host — the one composition root, controllers and the migrate verb
└─ Tests/      Scaffolds.ECommerce.{Unit, Integration, E2E, Architecture}Tests
```

Each project is foldered by bounded context, then aggregate, then feature, and every namespace mirrors its folder.

## Run

From the repository root:

1. Start a Postgres (or MySQL) server matching the connection strings of
   [`appsettings.Development.json`](Host/Scaffolds.ECommerce.Host/appsettings.Development.json);
   `Database:Provider` in [`appsettings.json`](Host/Scaffolds.ECommerce.Host/appsettings.json) picks the engine.
2. Set the secrets listed in [`secrets.template.json`](Host/Scaffolds.ECommerce.Host/secrets.template.json):
   `dotnet user-secrets set "ECommerce:Auth:Token:SigningKey" "<at least 32 characters>" --project scaffolds/Host/Scaffolds.ECommerce.Host`.
3. Migrate the schemas: `dotnet run --project scaffolds/Host/Scaffolds.ECommerce.Host -- migrate`.
4. Serve: `dotnet run --project scaffolds/Host/Scaffolds.ECommerce.Host`, then open `/scalar`.

To administer the first environment, list the external id of the administrator under
`ECommerce:Customers:BootstrapAdminExternalIds` before its first session.

## Test

```
dotnet build AxisFramework.slnx -c Release -warnaserror
dotnet test --solution AxisFramework.slnx -c Release
```

The integration and E2E tests start their databases with Testcontainers, so Docker must be running. The E2E
suite runs against the provider pinned in `ECommerceProviderPin.DefaultProvider`; the other provider's
collection skips visibly.

## Documentation map

| Where | What |
|---|---|
| `/scalar` on the running host | The API: vocabulary, bounded contexts, auth, every endpoint |
| [`site/guide/`](../site/guide/index.md) | How an Axis application is built, each convention linked to its code here |
| [`agents/axis-framework-dotnet.md`](../agents/axis-framework-dotnet.md) | The same conventions, written for a coding assistant |
