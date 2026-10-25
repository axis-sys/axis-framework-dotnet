using Scaffolds.ECommerce.SharedKernel.Auth;

namespace Scaffolds.ECommerce.Host.OpenApi;

internal static class ECommerceOpenApi
{
    public const string Title = "Scaffolds ECommerce API";

    public const string Description = """
        The reference application of the Axis framework: a small e-commerce whose bounded contexts are composed
        by one host.

        ## Vocabulary

        - **Product** — an item of the catalog, identified by its id and by a unique **SKU** (case-insensitive,
          kept in upper case), with a stock of units.
        - **Cart** — an id chosen by the client; checking a product out into a cart reserves its stock.
        - **Order** — submitted for a cart once its checked-out product is associated with it, and read back by its
          customer priced with the catalog's unit price and discount rate.
        - **Customer** — provisioned on the first session of an external identity.
        - **Validation code** — the one-time code emailed to prove ownership of the customer's email.
        - **Member** — a customer granted permission codes by an administrator; a disabled member holds none.

        ## API shape

        Every route is versioned in its path: `/api/v1/{resource}`, plural and kebab-case. A success answers
        `200`, `201` or, for a run that finishes later, `202`; a failure answers an RFC 7807
        `application/problem+json` body whose `errors` list carries stable error codes — branch on the codes,
        never on the messages.

        ## Authentication

        1. Sign in at Microsoft Entra or Google Accounts and send that token to `POST /api/v1/sessions`.
        2. Use the session access token it returns as the bearer of every other request.

        The session token is thin: it identifies the customer and carries only the coarse administrator role.
        Permissions (`module.action` codes, such as `catalog.register-products`) are resolved on every request
        from the Authorization context: a disabled member is always refused, an administrator holds every
        permission, and anyone else needs a membership granting the code — or gets `403`. The context caches
        each caller's grants and drops them as soon as an administrator changes them with
        `PUT /api/v1/members/{customerId}/grants`.

        ## Bounded contexts

        | Tag | What it owns |
        |---|---|
        | Auth | Sessions: the exchange of an external token for a session token. |
        | Authorization | Members and their grants, managed by administrators and read by every permission check. |
        | Catalog | Products, checkout and orders. |
        | EmailValidation | The validation of the customer's email. |
        | Admin | Operations reserved to administrators, such as schema migrations. |

        ## Runs that finish later

        Validating an email changes two bounded contexts, so it runs as a saga: `POST /api/v1/email-validations`
        answers `202` with the run's saga id, then `GET /api/v1/email-validations/{sagaId}` reports its status
        until a terminal one (`Completed`, `Compensated`, `Failed`, `Cancelled`), and
        `POST /api/v1/email-validations/{sagaId}/cancel` stops it gracefully: the stage in progress finishes, nothing
        is compensated, and a run that already finished answers `409`. Reading or cancelling a run is neither open
        to everyone nor reserved to whoever started it: it needs the permission of the validation runs
        (`email-validation.validate-email-runs`), and a caller without it gets the same `404` as for a run that
        does not exist.

        ## Tooling

        The schemas are migrated before the API serves: `dotnet Scaffolds.ECommerce.Host.dll migrate` applies every
        pending migration and exits with `0`, or with `1` when a migration fails — run it on each deploy and on the
        first run against an empty database. The web host never migrates on its own; administrators re-run the
        same idempotent migration with `POST /api/v1/database-migrations`.

        This reference (`/scalar`) and its OpenAPI documents (`/openapi/v1.json`) are served only in
        Development, or where `AxisOpenApi:Publish` is `true`.
        """;

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
}
