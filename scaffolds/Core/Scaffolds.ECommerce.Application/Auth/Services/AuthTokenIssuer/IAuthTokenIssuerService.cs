namespace Scaffolds.ECommerce.Application.Auth.Services.AuthTokenIssuer;

internal interface IAuthTokenIssuerService
{
    AxisResult<JwtAccessToken> IssueBootstrapToken(User user);
}
