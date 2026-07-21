using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Application.Auth;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Auth;

// Swapped in for the external-provider schemes (MsEntra/Google) by the factory, so no request ever
// reaches a real identity provider. A request authenticates by naming a test user in the X-Test-User
// header; the handler emits the same normalized claims the real claim mappers would produce.
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var header))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Enum.TryParse<EUserType>(header.ToString(), ignoreCase: true, out var user))
            return Task.FromResult(AuthenticateResult.Fail($"Unknown test user '{header}'."));

        List<Claim> claims = [];
        var externalId = TestUsers.ExternalId(user);
        if (!string.IsNullOrEmpty(externalId))
        {
            claims.Add(new Claim(AuthClaimTypes.ExternalId, externalId));
            claims.Add(new Claim(AuthClaimTypes.Email, TestUsers.Email(user)));
            claims.Add(new Claim(AuthClaimTypes.Name, TestUsers.Name(user)));
            claims.Add(new Claim(AuthClaimTypes.AuthProvider, Scheme.Name));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
