namespace Scaffolds.ECommerce.IntegrationTests;

internal sealed record FakeMember(
    CustomerId CustomerId,
    bool Disabled,
    IReadOnlyList<string> Permissions
) : IMemberEntityProperties;
