namespace Scaffolds.ECommerce.E2ETests.Fixtures.Http;

internal static class Routes
{
    public const string Sessions = "/api/v1/sessions";
    public const string Products = "/api/v1/products";
    public const string Orders = "/api/v1/orders";
    public const string EmailValidations = "/api/v1/email-validations";
    public const string EmailValidationCodes = $"{EmailValidations}/codes";
    public const string DatabaseMigrations = "/api/v1/database-migrations";
    public const string Members = "/api/v1/members";
    public const string OpenApiV1 = "/openapi/v1.json";

    public static string Product(string productId) => $"{Products}/{productId}";

    public static string ProductCheckout(string productId) => $"{Products}/{productId}/checkout";

    public static string Order(string orderId) => $"{Orders}/{orderId}";

    public static string EmailValidationRun(string sagaId) => $"{EmailValidations}/{sagaId}";

    public static string EmailValidationRunCancel(string sagaId) => $"{EmailValidations}/{sagaId}/cancel";

    public static string MemberGrants(string customerId) => $"{Members}/{customerId}/grants";
}
