namespace Scaffolds.ECommerce.Application.Customers;

public sealed class CustomersOptions
{
    public const string SectionName = "ECommerce:Customers";

    // External ids promoted to admin when their customer is first provisioned (dev/test bootstrap).
    public string[] BootstrapAdminExternalIds { get; init; } = [];
}
