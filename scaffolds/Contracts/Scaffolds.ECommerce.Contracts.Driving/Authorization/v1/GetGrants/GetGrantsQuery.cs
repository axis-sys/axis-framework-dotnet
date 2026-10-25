namespace Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.GetGrants;

public sealed record GetGrantsQuery : IAxisQuery<GetGrantsResponse>
{
    public string? CustomerId { get; init; }
}
