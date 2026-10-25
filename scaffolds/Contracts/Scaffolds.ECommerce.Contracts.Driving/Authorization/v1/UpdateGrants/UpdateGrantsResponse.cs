namespace Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;

/// <summary>The grants a member holds after the change.</summary>
public sealed record UpdateGrantsResponse : IAxisCommandResponse
{
    /// <summary>Id of the customer the grants belong to.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Whether the membership is disabled.</summary>
    public required bool Disabled { get; init; }

    /// <summary>The permission codes (<c>module.action</c>) the member holds.</summary>
    public required IReadOnlyList<string> Permissions { get; init; }
}
