namespace Scaffolds.ECommerce.Application.Authorization.Services.GrantsCache;

internal sealed record Grants(
    string CustomerId,
    bool IsAdministrator,
    bool IsMember,
    bool IsDisabled,
    IReadOnlyList<string> Permissions
);
