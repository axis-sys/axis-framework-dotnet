namespace Scaffolds.ECommerce.Application.Customers;

internal static class CustomersErrors
{
    // GetCustomer / MarkEmailValidated / RevertEmailValidated
    public const string CustomerIdInvalid = "CUSTOMER_ID_INVALID";

    // EnsureCustomer
    public const string ExternalIdRequired = "EXTERNAL_ID_REQUIRED";
    public const string EmailInvalid = "EMAIL_INVALID";
    public const string NameRequired = "NAME_REQUIRED";
    public const string ProviderRequired = "PROVIDER_REQUIRED";
}
