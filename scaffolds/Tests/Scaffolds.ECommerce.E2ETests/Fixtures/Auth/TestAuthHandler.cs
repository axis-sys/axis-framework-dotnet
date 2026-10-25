using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.SharedKernel.Auth;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Auth;

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string UserHeader = "X-Test-User";
    public const string SubjectHeader = "X-Test-Subject";
    public const string ProviderHeader = "X-Test-Provider";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!IsTheRequestedProvider())
            return Task.FromResult(AuthenticateResult.NoResult());

        if (Request.Headers.TryGetValue(SubjectHeader, out var subject))
            return Task.FromResult(Authenticated(subject.ToString(), TestUsers.NewCustomerEmail(subject.ToString()), TestUsers.NewCustomerName));

        if (!Request.Headers.TryGetValue(UserHeader, out var header))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Enum.TryParse<EUserType>(header.ToString(), ignoreCase: true, out var user))
            return Task.FromResult(AuthenticateResult.Fail($"Unknown test user '{header}'."));

        return Task.FromResult(Authenticated(TestUsers.ExternalId(user), TestUsers.Email(user), TestUsers.Name(user)));
    }

    private AuthenticateResult Authenticated(string externalId, string email, string name)
    {
        List<Claim> claims = [];
        if (!string.IsNullOrEmpty(externalId))
        {
            claims.Add(new Claim(AuthClaimTypes.ExternalId, externalId));
            claims.Add(new Claim(AuthClaimTypes.Email, email));
            claims.Add(new Claim(AuthClaimTypes.Name, name));
            claims.Add(new Claim(AuthClaimTypes.AuthProvider, Scheme.Name));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private bool IsTheRequestedProvider()
        => Request.Headers.TryGetValue(ProviderHeader, out var provider)
            ? string.Equals(provider.ToString(), Scheme.Name, StringComparison.Ordinal)
            : Scheme.Name == AuthSchemes.MsEntra;
}
