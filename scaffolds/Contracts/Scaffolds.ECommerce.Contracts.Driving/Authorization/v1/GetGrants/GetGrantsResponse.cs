namespace Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.GetGrants;

public sealed record GetGrantsResponse : IAxisQueryResponse
{
    public required string CustomerId { get; init; }

    public required bool IsAdministrator { get; init; }

    public required bool IsMember { get; init; }

    public required bool IsDisabled { get; init; }

    public required IReadOnlyList<string> Permissions { get; init; }
}
