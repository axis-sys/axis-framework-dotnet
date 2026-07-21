using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.UnitTests;

// Fake entity-property record (testing-fake-entity-property-records): arranges customer state without
// touching the internal domain record.
public sealed record FakeCustomer(
    CustomerId CustomerId,
    string Email,
    string Name,
    bool IsAdmin,
    string? ExternalId,
    string? Provider,
    bool EmailValidated
) : ICustomerEntityProperties;
