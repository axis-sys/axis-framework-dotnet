using Scaffolds.ECommerce.Domain.Authorization.Members;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;

public interface IMembersReaderPort
{
    Task<AxisResult<IMemberEntityProperties>> GetByCustomerIdAsync(CustomerId customerId);
}
