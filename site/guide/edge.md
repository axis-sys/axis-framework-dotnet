# HTTP edge

The HTTP edge is the thin layer of the host that turns a request into a facade call and the facade's `AxisResult`
into a response. It owns no business logic: it binds the route and the body, authenticates and authorizes the
caller, calls one facade method and renders the result. Everything an edge does is visible in a handful of
attributes and one expression per action, which is what keeps a hundred endpoints reviewable.

The reference application keeps its edge in
[`scaffolds/Host/Scaffolds.ECommerce.Host`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Program.cs):
one composition root, one `Controllers/{BoundedContext}/` folder per bounded context, and dedicated `Auth/`,
`Authorization/` and `OpenApi/` folders for the cross-cutting wiring. Authentication, URL versioning and OpenAPI
are conventions the edge converges on, helped by two framework packages:
[`AxisResult.HttpResponse`](https://github.com/axis-sys/axis-result-httpresponse) (rendering) and
[`AxisOpenApi`](https://github.com/axis-sys/axis-openapi) (versioning and the API reference).

## Map

| Section | You want to… |
|---|---|
| [Controller shape](#controller-shape) | write a controller: sealed `ControllerBase`, the attribute stack, one action per use case |
| [Controllers depend on facades only](#controllers-depend-on-facades-only) | know what a controller may inject |
| [Rendering `AxisResult`](#rendering-axisresult) | end an action, pick a success status, refuse a request outside MVC |
| [URL versioning](#url-versioning) | version a route and its contracts |
| [XML docs feed OpenAPI](#xml-docs-feed-openapi) | document an endpoint so the reference shows it |
| [The OpenAPI document as documentation hub](#the-openapi-document-as-documentation-hub) | describe the API once, in Scalar |
| [Authentication schemes](#authentication-schemes) | exchange an external token for a session token |
| [Permission authorization](#permission-authorization) | gate an endpoint on a `module.action` permission or on the administrator role |
| [Tenant scoping (multi-tenant applications)](#tenant-scoping-multi-tenant-applications) | resolve the tenant of a request |
| [Route-injected command properties](#route-injected-command-properties) | merge a route id into a command safely |
| [Saga endpoints](#saga-endpoints) | expose a cross-context process as 202 + polling + cancel |
| [Checklist for a new endpoint](#checklist-for-a-new-endpoint) | review an endpoint before merging |

---

## Controller shape

Every endpoint lives in a **sealed** controller that inherits `ControllerBase` (never the view-aware `Controller`)
and injects through a **primary constructor**. The class carries a fixed attribute stack, always in this order:

| # | Attribute | In the reference application |
|---|---|---|
| 1 | API-controller marker | `[ApiController]` |
| 2 | Bounded-context tag | `[Tags("Catalog")]` — groups the operations in the reference |
| 3 | Authorization | `[Authorize]`, or the policy / scheme form (`[Authorize(Policy = AuthorizationPolicies.Administrator)]`) |
| 4 | Tenant marker (optional) | only in multi-tenant applications, see [Tenant scoping](#tenant-scoping-multi-tenant-applications) |
| 5 | API version | `[ApiVersion("1.0")]` |
| 6 | Versioned route | `[Route("api/v{version:apiVersion}/products")]` — the version segment and a plural, kebab-case resource |

Actions follow one shape:

- **One action per use case**, named `{Verb}[{Noun}]Async`, expression-bodied, returning `Task<IActionResult>`.
- Each action declares its **HTTP verb attribute** and a **`ProducesResponseType`** for the success shape (and for
  every documented error, see [XML docs](#xml-docs-feed-openapi)).
- **Route parameters bind as `string`**, never as a domain id type, and are merged into the command or query with a
  `with` expression or an object initializer. Parsing the value object is the validator's job, not the controller's
  (see [Domain](domain.md)).
- Controllers live under `Controllers/{BoundedContext}/`, mirroring the bounded context they serve.

```csharp
[ApiController]
[Tags("Catalog")]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
public sealed class ProductsV1Controller(IProductsFacade productsFacade) : ControllerBase
{
    /// <summary>Reads a single product by id.</summary>
    /// <param name="productId">Id of the product to read.</param>
    /// <response code="200">The product.</response>
    /// <response code="400">The product id is not a valid id.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No product with that id.</response>
    [HttpGet("{productId}")]
    [ProducesResponseType<GetProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetProductAsync(string productId)
        => HttpContext.SendAsync(productsFacade.GetProductAsync(new GetProductQuery { ProductId = productId }));
}
```

Reference: [`ProductsV1Controller.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Catalog/ProductsV1Controller.cs)
and its siblings, such as [`OrdersV1Controller.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Catalog/OrdersV1Controller.cs).

**Allowed deviations**

- A controller protected by a policy or a specific scheme replaces the bare `[Authorize]` with that form — the
  administrative
  [`MembersV1Controller`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Authorization/MembersV1Controller.cs)
  and
  [`DatabaseMigrationsV1Controller`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Admin/DatabaseMigrationsV1Controller.cs)
  carry `[Authorize(Policy = AuthorizationPolicies.Administrator)]`.
- A purely diagnostic edge read may expose a synchronous action returning a plain `IActionResult`.

The conventions analyzers (`Axis.Conventions.Analyzers`) report a controller that is not sealed
([`AXIS0602`](analyzers.md#axis0602)); under `-warnaserror` that breaks the build.

## Controllers depend on facades only

A controller's only doorway into the core is a **driving facade** (`I{Entities}Facade`). The facade interface lives
in the versioned driving contracts (`Contracts.Driving/{BoundedContext}/{Aggregate}/v1/`), and its implementation
is `internal` to the facade adapter, one dispatch expression per method. The facade is what makes the internal
handlers reachable; injecting anything deeper bypasses the boundary (see [Architecture](architecture.md)).

| Do | Don't |
|---|---|
| Take only `I{Entities}Facade` constructor parameters | Inject `IAxisMediator` |
| Take several facades when the resource spans aggregates | Inject an `I{Entities}ReaderPort` / `I{Entities}WritePort` or a repository |
| Put an input that cannot travel through a CQRS contract (a multipart stream) behind a facade method | Reach an application service from the controller |

The single exception is a pure edge read with no use case behind it (reading the ambient identity accessor or a
claim): it may inject that accessor instead of a facade, provided the controller documents the deviation and still
renders through the `AxisResult` extension.

Two tests keep this honest: the architecture test
[`FacadeBoundaryTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/FacadeBoundaryTests.cs)
(`InjectsOnlyFacadesWhenScanningTheHostControllers`) and the E2E
[`CompositionTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/CompositionTests.cs),
which activates every controller from the production container. The facade registrations are in
[`Adapters.Driving.Facade/DependencyInjection.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driving.Facade/DependencyInjection.cs).

## Rendering `AxisResult`

The [`AxisResult`](https://github.com/axis-sys/axis-result) railway ends at the edge, in **one render call** on
`HttpContext` from [`AxisResult.HttpResponse`](https://github.com/axis-sys/axis-result-httpresponse). A controller
never looks at the result.

| Call | Renders |
|---|---|
| `HttpContext.SendAsync(task)` | `200` with the value as body (a valueless `AxisResult` answers the bare status), or the failure |
| `HttpContext.SendAsync(task, HttpStatusCode.Created)` | a non-200 success (`Created`, `Accepted`, `NoContent`); mirror it in `ProducesResponseType` |
| `HttpContext.SendAcceptedAsync(task, value => location)` | `202 Accepted` with the value as body, `Location` and `Retry-After` — the start of a run the client polls ([Saga endpoints](#saga-endpoints)) |

What the extension does for you:

- maps each `AxisErrorType` to its HTTP status (the table is in
  [Errors and types](https://github.com/axis-sys/axis-result/blob/main/docs/errors-and-types.md)) and, when a
  result carries several errors, answers the status of the most severe one — the edge owns zero mapping code;
- writes failures as RFC 7807 `application/problem+json` whose `errors` list carries the stable codes
  (`InternalServerError` codes stay out of the body unless `AxisResult:Http:ExposeInternalErrorCodes` is on);
- injects the request trace identifier (`traceId`) itself.

```csharp
public Task<IActionResult> SubmitOrderAsync([FromBody] SubmitOrderCommand command)
    => HttpContext.SendAsync(ordersFacade.SubmitOrderAsync(command), HttpStatusCode.Created);
```

| Do | Don't |
|---|---|
| End every action with `SendAsync` / `SendAcceptedAsync` | Test `IsSuccess` / `IsFailure`, call `Match`, or build `Ok` / `NotFound` / `Problem` from a result |
| Pass a non-200 success status as the second argument and declare it with `ProducesResponseType` | Return a bare `201` the OpenAPI document does not know about |
| Use the codes declared once as `AxisError` members of the `{Aggregate}Errors` class beside the driven ports, or the kernel's shared errors class | Mint an error code or a message at the edge |
| Lift a value computed at the edge onto the railway with `.Rop()` before rendering | Render an edge-computed value by hand |

Error codes are declared next to the driven ports, for example
[`CustomerErrors.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Customers/Customers/CustomerErrors.cs)
and the kernel's
[`SharedErrors.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Shared/SharedErrors.cs)
(see [Style](style.md) for the errors-class shape). Exceptions never reach the edge: the `Try` boundary keeps them
off the railway ([Exceptions at the boundary](https://github.com/axis-sys/axis-result/blob/main/docs/boundary-and-try.md)).

### Refusing a request outside MVC

Middleware and authorization filters run before an action exists, so they cannot return an `IActionResult` from
`SendAsync`. They still answer with the same ProblemDetails shape (`type`, `title`, `status`, `detail`, `errors`,
`traceId`), through the public helpers of `AxisResult.HttpResponse`, all built on `AxisProblemDetailsBuilder`:

| Where | Helper |
|---|---|
| An `IAsyncAuthorizationFilter` | `context.Result = context.HttpContext.ToProblemDetailsResult(error)` |
| A middleware | `await context.WriteProblemDetailsAsync(error)` — writes the response itself |
| A policy requirement | `new AxisErrorRequirement(error, user => ...)` plus `services.AddAxisAuthorizationProblemDetails()` |
| Anything custom | `AxisProblemDetailsBuilder.Build(errors, traceId)` — never a hand-rolled status/shape mapping |

Codes that exist only at the HTTP edge (no core equivalent) come from a small edge-owned `{Concern}Errors` class,
never from inline string literals:
[`Authorization/AuthorizationErrors.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Authorization/AuthorizationErrors.cs).
Because the controller extension and these helpers share one builder, controllers and out-of-band checks cannot
drift apart on status or shape.

## URL versioning

The version lives in a **URL path segment**. `AddAxisUrlVersioning()` from
[`AxisOpenApi`](https://github.com/axis-sys/axis-openapi) sets the default version (1.0), assumes it when the caller
omits it, reports the served versions in the response headers, reads the version only from the path, and makes the
API explorer group documents by `v1`, `v2`… with the version substituted back into the documented routes.

```csharp
builder.Services
    .AddAxisUrlVersioning()
    .AddAxisOpenApi(ECommerceOpenApi.Configure, typeof(Program).Assembly, typeof(IProductsFacade).Assembly);
```

| Element | Convention | Example |
|---|---|---|
| Controller | declares `[ApiVersion]` and `api/v{version}/{resource}`, the version right after the fixed `api` prefix | `[Route("api/v{version:apiVersion}/email-validations")]` |
| Class name | carries the version suffix, so a later version coexists as a separate file | `EmailValidationsV1Controller` |
| Driving contracts | mirror the version in the namespace `{BC}.{Aggregate}.v1.{UseCase}` | `Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct` |

References: [`Program.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Program.cs),
[`EnsureCustomerCommand.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Customers/v1/EnsureCustomer/EnsureCustomerCommand.cs).

## XML docs feed OpenAPI

The OpenAPI document is the living API documentation, so the `///` comments on controllers are **contract
documentation**, not decoration. They are the one place the codebase expects prose: elsewhere comments stay
minimal.

- The controller class carries a `<summary>` that frames the resource: who may call it, whether it is synchronous
  or answers a saga run.
- Every action carries a `<summary>`; a non-obvious flow adds `<remarks>` (polling instructions, a multi-step
  protocol, idempotency).
- Every action documents its parameters with `<param>` and its outcomes with `<response code="...">` lines,
  **error codes included** (400, 401, 403, 404, 409, 422…).
- **Every documented status is also declared with `ProducesResponseType`.** The `AxisOpenApi` XML transformer only
  publishes a `<response>` line for a status the action declares; an undeclared one disappears from the document
  without a warning. The attribute declares the response, the XML describes it.
- Literals go in `<c>...</c>`; the documentation language is the project's (English in the reference application).
- XML documentation-file generation is switched on once, in the shared `Directory.Build.props`, for every shipped
  project; no project opts out. `AddAxisOpenApi` then reads the XML files of the assemblies you pass it — the host
  (operation summaries, remarks, parameters, responses) and the driving contracts (schema and property
  descriptions, `<example>` values).

```csharp
/// <summary>Registers a new product in the catalog.</summary>
/// <remarks>
/// Idempotent by SKU: registering a SKU that is already in the catalog answers the product registered
/// under it. The SKU is case-insensitive and kept in upper case.
/// </remarks>
/// <param name="command">SKU, name and initial stock of the product.</param>
/// <response code="201">Product registered, or the product already registered under that SKU.</response>
/// <response code="400">The SKU, the name or the initial stock violates a validation rule.</response>
/// <response code="401">Missing or invalid access token.</response>
/// <response code="403">Authenticated but lacking the permission to register products.</response>
[HttpPost]
[RequirePermission(PermissionCodes.CatalogRegisterProducts)]
[ProducesResponseType<RegisterProductResponse>(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
```

The E2E test
[`OpenApiDocumentTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Documentation/OpenApiDocumentTests.cs)
pins every operation of the reference application with its summary and its exact set of response codes.

## The OpenAPI document as documentation hub

One rendered reference over the transformer-shaped OpenAPI document is the project's documentation hub. With
`AxisOpenApi` that reference is Scalar at `/scalar`, next to the raw documents at
`/openapi/v1.json`; the tool is a choice, the single hub is the convention. The README stays lean and links to it.

| Piece | Convention | In the reference application |
|---|---|---|
| Info description | The single narrative document of the API: domain vocabulary, API shape, the authentication handshake, bounded contexts, the saga model, tooling | `ECommerceOpenApi.Description`, a `const string` handed to `AxisOpenApiOptions.Description` |
| Transformers | `AddAxisOpenApi` always registers the info transformer; when `AxisOpenApiOptions` declares schemes (`BearerSchemes`, `OAuth2Schemes`) it also registers the security-scheme document transformer and the per-operation security transformer | three bearer schemes, `DefaultSecurityScheme = AuthSchemes.ECommerce` |
| Scheme names | Constants declared once by the application, shared with the authentication registration | [`AuthSchemes.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Auth/AuthSchemes.cs) |
| Preferred scheme of the UI | The same constant | `app.MapAxisOpenApi(configure: null, AuthSchemes.ECommerce)` |
| Exposure | Documents and UI are mapped **only in Development**; production exposes neither unless an environment opts in with `AxisOpenApi:Publish = true` | stated at the end of the description itself |
| Provider quirks | OAuth-flow workarounds for the docs UI live in the flow configuration (the scheme options or the `MapAxisOpenApi` configure callback), never in controllers | — |

```csharp
public static void Configure(AxisOpenApiOptions options)
{
    options.Title = Title;
    options.Description = Description;
    options.DefaultSecurityScheme = AuthSchemes.ECommerce;
    options.BearerSchemes.Add(new AxisOpenApiBearerScheme
    {
        Name = AuthSchemes.ECommerce,
        Description = "The session access token issued by POST /api/v1/sessions.",
    });
    options.BearerSchemes.Add(new AxisOpenApiBearerScheme
    {
        Name = AuthSchemes.MsEntra,
        Description = "A Microsoft Entra token, accepted only by POST /api/v1/sessions.",
    });
    options.BearerSchemes.Add(new AxisOpenApiBearerScheme
    {
        Name = AuthSchemes.Google,
        Description = "A Google Accounts token, accepted only by POST /api/v1/sessions.",
    });
}
```

Reference: [`OpenApi/ECommerceOpenApi.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/OpenApi/ECommerceOpenApi.cs)
— read its `Description` for a complete example of what the narrative covers.

## Authentication schemes

Authentication is a **two-stage handshake** wired in the composition root:

1. A named scheme per external identity provider validates the provider's token. Only the **token-exchange
   endpoint** accepts it.
2. The **default** named scheme validates the API's own session token. Every other protected endpoint
   authenticates with it.

```csharp
services
    .AddAuthentication(AuthSchemes.ECommerce)
    .AddJwtBearer(AuthSchemes.ECommerce, _ => { })
    .AddJwtBearer(AuthSchemes.MsEntra, _ => { })
    .AddJwtBearer(AuthSchemes.Google, _ => { });

services
    .AddOptions<AuthTokenOptions>()
    .Bind(configuration.GetSection(AuthTokenOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

- At least two named schemes are registered, and the default is the session-token scheme.
- Only the token-exchange action names the external schemes, with an explicit
  `[Authorize(AuthenticationSchemes = AuthSchemes.ExternalIdentities)]` on that one action
  ([`SessionsV1Controller.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Auth/SessionsV1Controller.cs));
  a session token presented there is refused.
- Token refresh, logout and any credential-in-the-body exchange are **anonymous by design**: the access token is
  absent or already expired when they run.
- Every scheme's option block binds from configuration with data-annotation validation and **validate-on-start**, so
  a missing setting stops the host instead of failing the first request. Signing keys come from user secrets or the
  environment, never from committed settings (`appsettings.json` ships `SigningKey` empty;
  [`secrets.template.json`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/secrets.template.json)
  lists the keys to supply).
- The session token is **thin**: identity claims and coarse flags only (the reference application signs the
  customer id, email, name, external id, provider and, for administrators, a single role claim). Tenant and
  permission resolution stay server-side, which is what [Permission authorization](#permission-authorization) and
  [Tenant scoping](#tenant-scoping-multi-tenant-applications) rely on.
- `app.UseAxisAmbientIdentity(AuthClaimTypes.CustomerId)` (from `AxisResult.HttpResponse`), placed after
  `UseAuthentication` and before `UseAuthorization`, copies the caller's id into the mediator's ambient context, so
  handlers read the identity instead of receiving it as a parameter.

The concrete identity providers are an application choice; the convention is the split between external-token
exchange and own-session resource access. The external schemes' options are configured by a swappable driven
adapter,
[`Adapters.Driven.AuthProviders/DependencyInjection.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.AuthProviders/DependencyInjection.cs).
Tests may re-point the external schemes to a test handler while the session-token scheme stays real, so
session-token validation keeps being exercised (see [Testing](testing.md#end-to-end-tests-with-webapplicationfactory)).

References: [`Auth/DependencyInjection.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Auth/DependencyInjection.cs),
[`AuthTokenIssuerService.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/Services/AuthTokenIssuer/AuthTokenIssuerService.cs),
[`AuthenticationCompositionTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/AuthenticationCompositionTests.cs)
(each option block refuses to start without one of its required settings).

## Permission authorization

Granular authorization never travels in the token. An endpoint declares the `module.action` code it needs with a
permission attribute; **one global MVC authorization filter** resolves the caller's grants through the
authorization bounded context's read-model facade and decides.

```csharp
[HttpPost]
[RequirePermission(PermissionCodes.CatalogRegisterProducts)]
[ProducesResponseType<RegisterProductResponse>(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public Task<IActionResult> RegisterProductAsync([FromBody] RegisterProductCommand command)
    => HttpContext.SendAsync(productsFacade.RegisterProductAsync(command), HttpStatusCode.Created);
```

The filter's whole policy is one expression over the facts the authorization facade reports:

```csharp
private static bool IsGranted(GetGrantsResponse grants, string code)
    => !grants.IsDisabled && (grants.IsAdministrator || (grants.IsMember && grants.Permissions.Contains(code)));
```

| Piece | Convention |
|---|---|
| Codes | Constants of a `PermissionCodes` class (`catalog.register-products`); attributes never carry string literals |
| Filter | Scoped, registered **once globally** (`mvc.Filters.AddService<PermissionAuthorizationFilter>()`), a no-op on endpoints without the attribute |
| Identity | Read from the caller's identity claim; a claim that does not parse short-circuits **401** |
| Refusal | A failed grants lookup or a missing permission short-circuits **403**; both refusals are ProblemDetails with edge-owned codes |
| Facts vs. policy | The facade reports facts (administrator, member, disabled, permission codes); the policy lives in the filter: a disabled caller is always denied, an administrator bypasses the per-permission check, everyone else needs a membership holding the code |
| Read model | A cache-aside over `IAxisCache.GetOrCreateAsync` ([`AxisCache`](https://github.com/axis-sys/axis-cache)), evicted by the consumer of the event the grants use case publishes |
| Tenant | The check is tenant-scoped, so a permission-gated endpoint implies the tenant marker — except in single-tenant applications (see below) |
| Administrative controllers | A claim-based administrator policy declared in the authorization setup (`AxisErrorRequirement` over the role claim), with no per-request lookup in the authorization context |
| Location | Attribute, filter, policies and header names live in a dedicated edge folder (`Host/Authorization/`), not in `Program.cs` |

The gate is the edge's: handlers do not re-authorize what the filter already decided (see
[Architecture](architecture.md)).

**Exceptions**

- **Single-tenant applications** have no tenant scope: the filter resolves the grants by caller alone and a
  permission-gated endpoint carries no tenant marker. The reference application is single-tenant.
- Global read-only vocabularies may require only bare authentication, with no permission code.
- On a **saga run resource**, a missing permission answers **404**, the same answer as an unknown run, instead of 403
  — the check is identical, only the answer changes, so the existence of a run never leaks. The reference
  application flags it on the attribute: `[RequirePermission(PermissionCodes.ValidateEmailRuns, ConcealsSagaRun = true)]`.

References: [`PermissionAuthorizationFilter.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Authorization/PermissionAuthorizationFilter.cs),
[`RequirePermissionAttribute.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Authorization/RequirePermissionAttribute.cs),
[`Authorization/DependencyInjection.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Authorization/DependencyInjection.cs),
[`PermissionCodes.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Authorization/PermissionCodes.cs),
[`GetGrantsHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/UseCases/GetGrants/v1/GetGrantsHandler.cs),
[`GrantsCacheService.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Services/GrantsCache/GrantsCacheService.cs),
[`UpdateGrantsHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/UseCases/UpdateGrants/v1/UpdateGrantsHandler.cs)
(publishes `GrantsUpdatedEvent` in its unit of work),
[`GrantsUpdatedEventHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Events/GrantsUpdatedEventHandler.cs)
(evicts the cached grants), and the E2E proofs
[`RegisterProductTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/Catalog/RegisterProductTests.cs)
and
[`UpdateGrantsTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/Authorization/UpdateGrantsTests.cs)
(a grant changed by an administrator takes effect on the member's next request).

## Tenant scoping (multi-tenant applications)

> [!NOTE]
> This applies only to multi-tenant applications. The reference application is single-tenant and has no tenant
> middleware; there is no reference code for this section.

In a multi-tenant application the tenant is **fully ambient**: no command or query carries a tenant field from the
controller.

- Endpoints that operate on tenant data carry a **tenant marker attribute**, discovered through endpoint metadata;
  endpoints without it pass through the middleware untouched.
- A **middleware** registered after routing, authentication and authorization, and before endpoint execution, does
  the work — so the endpoint and its metadata are selected, the claims exist, and the tenant is resolved before any
  filter or handler runs.
- The tenant **header name is a constant**; its value is trimmed and normalized before use.
- Refusals, all as ProblemDetails: a missing or blank header on a marked endpoint → **400**; an invalid slug →
  **400**; a tenant the caller's grants deny → **403** (administrators bypass).
- Membership is validated through the authorization facade — the thin token carries no tenant claim — using the
  same grants model and cache as the permission filter.
- The middleware publishes the tenant through the **ambient accessor**; controllers and handlers read it only
  there, and nothing outside the middleware reads the header.
- Tenant-neutral surfaces omit the marker on purpose: session and token endpoints, tenant listing, identity,
  administrative and global-vocabulary endpoints.
- Sagas re-establish the ambient tenant from their payload; the middleware covers only the HTTP request.

## Route-injected command properties

When a controller merges a route value into a command with a `with` expression, the target property is
**`[JsonIgnore]`**, nullable and `init`. Otherwise the body could also send it: the deserializer fills it, the
`with` expression overwrites it, and the body value is dropped silently — the caller cannot tell which id won. The
route is the single source of truth for a route-bound id.

```csharp
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

```csharp
[HttpPost("{productId}/checkout")]
public Task<IActionResult> CheckoutProductAsync(string productId, [FromBody] CheckoutProductCommand command)
    => HttpContext.SendAsync(productsFacade.CheckoutProductAsync(command with { ProductId = productId }));
```

A command whose id only ever travels in the body (no route segment) keeps the property serializable. A query built
entirely from the route (`new GetOrderQuery { OrderId = orderId }`) has no body to collide with. The conventions
analyzers report a route-injected property without `[JsonIgnore]` ([`AXIS0605`](analyzers.md#axis0605)).

References: [`CheckoutProductCommand.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/CheckoutProduct/CheckoutProductCommand.cs),
[`UpdateGrantsCommand.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Authorization/v1/UpdateGrants/UpdateGrantsCommand.cs).

## Saga endpoints

A use case that crosses bounded contexts runs as a saga ([`AxisSaga`](https://github.com/axis-sys/axis-saga)) and
is exposed with the **accepted / poll / cancel** pattern. Single-context use cases stay synchronous `200`/`201` and
never fake asynchrony or return a saga id.

| Endpoint | Convention |
|---|---|
| `POST /email-validations` (start) | Renders **202 Accepted** with `SendAcceptedAsync` and declares a 202 `ProducesResponseType`; the body carries the **saga id** (and the accepted status), `Location` points to the run resource, `Retry-After` paces the polls |
| `GET /email-validations/{sagaId}` (run) | The run status and its outcome (here `EmailValidated`, `ErrorCode`); a batch saga adds per-item outcomes and pagination. An unknown run, a run of another saga type or (multi-tenant) of another tenant answers **404** |
| `POST /email-validations/{sagaId}/cancel` | A graceful stop: no rollback of finished work; answers **200** with `Cancelling` or `Cancelled`, **409** for a run that already finished |
| Drill-down reads | Hang off the run resource, never a separate top-level route |

```csharp
[HttpPost]
[ProducesResponseType<ValidateEmailResponse>(StatusCodes.Status202Accepted)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public Task<IActionResult> ValidateEmailAsync([FromBody] ValidateEmailCommand command)
    => HttpContext.SendAcceptedAsync(
        emailValidationFacade.ValidateEmailAsync(command),
        accepted => Url.RouteUrl(ValidationRunRoute, new { sagaId = accepted.SagaId }));

[HttpGet("{sagaId}", Name = ValidationRunRoute)]
[RequirePermission(PermissionCodes.ValidateEmailRuns, ConcealsSagaRun = true)]
[ProducesResponseType<GetValidationStatusResponse>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public Task<IActionResult> GetValidationStatusAsync(string sagaId)
    => HttpContext.SendAsync(emailValidationFacade.GetValidationStatusAsync(new GetValidationStatusQuery { SagaId = sagaId }));
```

**Who may read a run.** Reading a run is neither open to every authenticated caller nor limited to whoever started
it. Each saga type declares its own permission code (a `PermissionCodes` constant); the GET, every drill-down and the
cancel require it, through the same attribute and global filter as any
[permission-gated endpoint](#permission-authorization). A caller without it gets **the same 404 as for an unknown
run** — the E2E test compares both bodies, trace id aside.

**Map the saga's codes to your own errors.** `AxisSagaErrors` exposes the saga outcome codes only as `const string`s,
with no `AxisError` to reuse, so the application — the owner of its HTTP contract — maps **every** code the saga
mediator can return to its own errors class. The unknown saga becomes the application's unknown-run error (the same
one the filter answers), and any unexpected code becomes a generic run failure (`InternalServerError`). No framework
code reaches the edge.

```csharp
public static AxisError RunErrorOf(AxisError error) => error.Code switch
{
    AxisSagaErrors.SagaInstanceNotFound => SharedErrors.SagaRunNotFound,
    AxisSagaErrors.NotCancellable => SharedErrors.SagaRunNotCancellable,
    AxisSagaErrors.PersistenceFailed => SharedErrors.SagaRunStoreFailed,
    _ => SharedErrors.SagaRunFailed,
};
```

Document the 202/poll contract for API consumers in the OpenAPI description as well (the reference application's
"Runs that finish later" section).

References: [`EmailValidationsV1Controller.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/EmailValidation/EmailValidationsV1Controller.cs),
[`ValidateEmailResponse.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/EmailValidation/v1/ValidateEmail/ValidateEmailResponse.cs),
[`ValidateEmailRuns.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/ValidateEmailRuns.cs),
[`SharedErrors.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Shared/SharedErrors.cs),
[`CancelValidationHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/UseCases/CancelValidation/v1/CancelValidationHandler.cs)
(`IAxisSagaMediator.CancelAsync`, the graceful stop), and the tests
[`GetValidationStatusTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/EmailValidation/GetValidationStatusTests.cs),
[`CancelValidationTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/EmailValidation/CancelValidationTests.cs)
and
[`EmailValidationRunsDualDbTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/EmailValidationRunsDualDbTests.cs)
(an unregistered saga reaches the caller as the generic run failure, never as the framework code). The orchestration
behind these endpoints is covered in [Architecture](architecture.md).

## Checklist for a new endpoint

- [ ] Sealed `ControllerBase` with a primary constructor, in `Controllers/{BoundedContext}/`, class suffixed `V{n}Controller`.
- [ ] Attribute stack in order: `[ApiController]`, `[Tags]`, `[Authorize]` (or policy/scheme), tenant marker if multi-tenant, `[ApiVersion]`, `[Route("api/v{version:apiVersion}/{plural-kebab-resource}")]`.
- [ ] Constructor takes facades only.
- [ ] One expression-bodied `{Verb}[{Noun}]Async` action per use case, ending in `SendAsync` / `SendAcceptedAsync`; no branching on the result.
- [ ] Route parameters as `string`; a route value merged with `with` targets a `[JsonIgnore]`, nullable, `init` property.
- [ ] `<summary>` on the class and the action, `<remarks>` for non-obvious flows, `<param>` per parameter, `<response code>` per outcome — each with its `ProducesResponseType`.
- [ ] A permission-gated action uses `[RequirePermission(PermissionCodes.X)]`; a saga run resource conceals a missing permission as 404.
- [ ] A cross-context process answers 202 with the saga id and exposes GET run + POST cancel.
- [ ] The E2E journey exists: happy path, anonymous 401, and 403 plus the privileged bypass where a permission applies (see [Testing](testing.md#the-controller-coverage-gate)).

## See also

- [Testing](testing.md) — the E2E gate every controller must pass, and how sagas are tested at the edge.
- [Architecture](architecture.md) — the facade boundary, the composition root, where authorization is decided.
- [Style](style.md) — error classes and naming.
- [`AxisResult.HttpResponse`](https://github.com/axis-sys/axis-result-httpresponse) · [`AxisOpenApi`](https://github.com/axis-sys/axis-openapi) · [`AxisResult` docs](https://github.com/axis-sys/axis-result/blob/main/docs/index.md)
