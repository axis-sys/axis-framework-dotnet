using Scaffolds.ECommerce.Domain.Authorization.Members;

namespace Scaffolds.ECommerce.UnitTests;

internal sealed record FakeMember(
    CustomerId CustomerId,
    bool Disabled,
    IReadOnlyList<string> Permissions
) : IMemberEntityProperties;
