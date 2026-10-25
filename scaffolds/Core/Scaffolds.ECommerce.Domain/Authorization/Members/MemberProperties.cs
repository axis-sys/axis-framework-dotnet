using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Domain.Authorization.Members;

internal sealed record MemberProperties(
    CustomerId CustomerId,
    bool Disabled,
    IReadOnlyList<string> Permissions
) : IMemberEntityProperties;
