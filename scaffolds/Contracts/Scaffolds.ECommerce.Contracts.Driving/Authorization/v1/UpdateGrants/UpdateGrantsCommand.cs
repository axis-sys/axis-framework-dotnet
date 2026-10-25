namespace Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;

/// <summary>
/// Replaces the grants of a member: whether the membership is disabled and the permission codes it holds. The
/// first grant of a customer makes it a member.
/// </summary>
public sealed record UpdateGrantsCommand : IAxisCommand<UpdateGrantsResponse>
{
    [JsonIgnore]
    public string? CustomerId { get; init; }

    /// <summary>Whether the membership is disabled; a disabled member is refused every permission.</summary>
    /// <example>false</example>
    public bool? Disabled { get; init; }

    /// <summary>The permission codes (<c>module.action</c>) the member holds, replacing the previous ones; empty revokes them all.</summary>
    /// <example>["catalog.register-products"]</example>
    public IReadOnlyList<string>? Permissions { get; init; }
}
