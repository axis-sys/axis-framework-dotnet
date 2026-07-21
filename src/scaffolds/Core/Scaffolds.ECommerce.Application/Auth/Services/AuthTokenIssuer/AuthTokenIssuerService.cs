using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Scaffolds.ECommerce.SharedKernel.ValueObjects;

namespace Scaffolds.ECommerce.Application.Auth.Services.AuthTokenIssuer;

public interface IAuthTokenIssuerService
{
    AxisResult<JwtAccessToken> IssueBootstrapToken(User user);
}

internal sealed class AuthTokenIssuerService(
    IOptions<AuthTokenOptions> options,
    TimeProvider timeProvider
) : IAuthTokenIssuerService
{
    private readonly AuthTokenOptions _options = options.Value;

    public AxisResult<JwtAccessToken> IssueBootstrapToken(User user)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(_options.AccessTokenLifetimeSeconds);

        List<Claim> claims =
        [
            new("jti", Guid.CreateVersion7().ToString("N")),
            new(AuthClaimTypes.CustomerId, user.UserId),
            new(AuthClaimTypes.Email, user.Email),
            new(AuthClaimTypes.Name, user.Name),
        ];
        
        if (!string.IsNullOrEmpty(user.ExternalId))
            claims.Add(new(AuthClaimTypes.ExternalId, user.ExternalId));

        if (!string.IsNullOrEmpty(user.Provider))
            claims.Add(new(AuthClaimTypes.AuthProvider, user.Provider));

        if (user.IsSystemAdim)
            claims.Add(new Claim(AuthClaimTypes.Permission, "admin"));

        SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(_options.SigningKey));
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new JwtAccessToken(token, expiresAt);
    }
}
