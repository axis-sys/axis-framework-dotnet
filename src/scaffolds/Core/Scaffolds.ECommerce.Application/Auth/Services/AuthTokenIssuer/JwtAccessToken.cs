namespace Scaffolds.ECommerce.Application.Auth.Services.AuthTokenIssuer;

public sealed record JwtAccessToken(
    string AccessToken,
    DateTimeOffset ExpiresAt
);
