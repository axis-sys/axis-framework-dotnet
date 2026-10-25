namespace Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;

public static class CustomerErrors
{
    public const string ExternalIdRequired = "EXTERNAL_ID_REQUIRED";
    public const string EmailInvalid = "EMAIL_INVALID";
    public const string ProviderRequired = "PROVIDER_REQUIRED";
    public static readonly AxisError CustomerEmailAlreadyExists = AxisError.Conflict("CUSTOMER_EMAIL_ALREADY_EXISTS");

    public static readonly AxisError CustomerNotFound = AxisError.NotFound("CUSTOMER_NOT_FOUND");
}
