namespace Scaffolds.ECommerce.IntegrationTests;

internal sealed record FakeCustomer(
    CustomerId CustomerId,
    string Email,
    string Name,
    bool IsAdmin,
    string? ExternalId,
    string? Provider,
    bool EmailValidated
) : ICustomerEntityProperties;
