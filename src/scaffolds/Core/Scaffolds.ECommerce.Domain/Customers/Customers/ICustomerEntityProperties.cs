using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Domain.Customers.Customers;

public interface ICustomerEntityProperties
{
    CustomerId CustomerId { get; }
    string Email { get; }
    string Name { get; }
    bool IsAdmin { get; }
    string? ExternalId { get; }
    string? Provider { get; }
    bool EmailValidated { get; }
}
