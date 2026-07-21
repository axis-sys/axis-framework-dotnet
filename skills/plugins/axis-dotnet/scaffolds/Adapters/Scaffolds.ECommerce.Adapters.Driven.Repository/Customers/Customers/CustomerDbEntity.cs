using System.Data.Common;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;

internal sealed record CustomerDbEntity(
    CustomerId CustomerId,
    string Email,
    string Name,
    bool IsAdmin,
    string? ExternalId,
    string? Provider,
    bool EmailValidated
) : ICustomerEntityProperties
{
    internal static CustomerDbEntity FromReader(DbDataReader reader)
        => new(
            (CustomerId)reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetBoolean(6)
        );
}
