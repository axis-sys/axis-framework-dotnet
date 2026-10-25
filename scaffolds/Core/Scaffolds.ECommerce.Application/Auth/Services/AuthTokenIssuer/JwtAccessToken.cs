namespace Scaffolds.ECommerce.Application.Auth.Services.AuthTokenIssuer;

internal sealed record JwtAccessToken(
    string AccessToken,
    DateTimeOffset ExpiresAt
);
