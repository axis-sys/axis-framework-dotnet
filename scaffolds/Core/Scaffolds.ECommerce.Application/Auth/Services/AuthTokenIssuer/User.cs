namespace Scaffolds.ECommerce.Application.Auth.Services.AuthTokenIssuer;

internal sealed record User(
    string UserId,
    string Email,
    string Name,
    string? ExternalId,
    string? Provider,
    bool IsSystemAdmin
);
