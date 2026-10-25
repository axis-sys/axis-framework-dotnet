using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Domain.Authorization.Members;

public interface IMemberEntityProperties
{
    CustomerId CustomerId { get; }
    bool Disabled { get; }
    IReadOnlyList<string> Permissions { get; }
}
