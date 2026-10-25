using Scaffolds.ECommerce.Domain.Authorization.Members;

namespace Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;

public interface IMembersWritePort
{
    Task<AxisResult<IMemberEntityProperties>> CreateAsync(IMemberEntityProperties member);
    Task<AxisResult<IMemberEntityProperties>> UpdateAsync(IMemberEntityProperties member);
}
