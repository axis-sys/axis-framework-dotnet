namespace Scaffolds.ECommerce.Application.Customers;

public sealed class CustomersOptions
{
    public const string SectionName = "ECommerce:Customers";

    public string[] BootstrapAdminExternalIds { get; init; } = [];
}
