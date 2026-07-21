using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Domain.Customers.Customers;

internal sealed record CustomerProperties(
    CustomerId CustomerId,
    string Email,
    string Name,
    bool IsAdmin,
    string? ExternalId,
    string? Provider,
    bool EmailValidated
) : ICustomerEntityProperties;
