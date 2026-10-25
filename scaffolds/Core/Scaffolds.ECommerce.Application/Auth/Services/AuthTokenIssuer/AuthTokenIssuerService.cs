using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Contracts.Driven.Auth;
using Scaffolds.ECommerce.SharedKernel.Auth;

namespace Scaffolds.ECommerce.Application.Auth.Services.AuthTokenIssuer;

internal sealed class AuthTokenIssuerService(
    IAccessTokenSignerPort accessTokenSigner,
    IOptions<AuthTokenOptions> options,
    TimeProvider timeProvider
) : IAuthTokenIssuerService
{
    public AxisResult<JwtAccessToken> IssueBootstrapToken(User user)
    {
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = issuedAt.AddSeconds(options.Value.AccessTokenLifetimeSeconds);

        return accessTokenSigner
            .Sign(new AccessTokenDescriptor(ClaimsOf(user), issuedAt, expiresAt))
            .Map(token => new JwtAccessToken(token, expiresAt));
    }

    private static Dictionary<string, string> ClaimsOf(User user)
    {
        Dictionary<string, string> claims = new()
        {
            [AuthClaimTypes.CustomerId] = user.UserId,
            [AuthClaimTypes.Email] = user.Email,
            [AuthClaimTypes.Name] = user.Name,
        };

        if (!string.IsNullOrEmpty(user.ExternalId))
            claims[AuthClaimTypes.ExternalId] = user.ExternalId;

        if (!string.IsNullOrEmpty(user.Provider))
            claims[AuthClaimTypes.AuthProvider] = user.Provider;

        if (user.IsSystemAdmin)
            claims[AuthClaimTypes.Role] = AuthRoles.Administrator;

        return claims;
    }
}
