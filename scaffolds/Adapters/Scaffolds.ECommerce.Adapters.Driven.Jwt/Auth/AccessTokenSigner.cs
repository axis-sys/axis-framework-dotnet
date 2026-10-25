using System.Text;
using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Contracts.Driven.Auth;

namespace Scaffolds.ECommerce.Adapters.Driven.Jwt.Auth;

internal sealed class AccessTokenSigner(IOptions<AuthTokenOptions> options) : IAccessTokenSignerPort
{
    private readonly AuthTokenOptions _options = options.Value;
    private readonly AxisJwtHs256 _jwt = new(Encoding.UTF8.GetBytes(options.Value.SigningKey));

    public AxisResult<string> Sign(AccessTokenDescriptor descriptor)
        => _jwt.Sign(AxisJwtClaims.Standard(
            _options.Issuer,
            _options.Audience,
            descriptor.IssuedAt,
            descriptor.ExpiresAt - descriptor.IssuedAt,
            descriptor.Claims.Select(claim => KeyValuePair.Create<string, object?>(claim.Key, claim.Value))));
}
