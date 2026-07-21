namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;

public static class CustomersColumns
{
    public const string CustomerId = "CUSTOMER_ID";
    public const string Email = "EMAIL";
    public const string Name = "NAME";
    public const string IsAdmin = "IS_ADMIN";
    public const string ExternalId = "EXTERNAL_ID";
    public const string Provider = "PROVIDER";
    public const string EmailValidated = "EMAIL_VALIDATED";
    public const string All = $"{CustomerId}, {Email}, {Name}, {IsAdmin}, {ExternalId}, {Provider}, {EmailValidated}";
}
