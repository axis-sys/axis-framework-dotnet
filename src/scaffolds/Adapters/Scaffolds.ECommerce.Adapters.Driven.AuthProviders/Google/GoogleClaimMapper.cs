using System.Security.Claims;
using Scaffolds.ECommerce.Application.Auth;

namespace Scaffolds.ECommerce.Adapters.Driven.AuthProviders.Google;

// Normalizes Google Accounts claims into the app's own claim names, so everything after
// authentication (the IExternalIdentityContext port included) is provider-agnostic.
internal static class GoogleClaimMapper
{
    public static void MapCustomClaims(ClaimsIdentity identity)
    {
        var subject = identity.FindFirst("sub");
        if (subject is not null && identity.FindFirst(AuthClaimTypes.ExternalId) is null)
            identity.AddClaim(new Claim(AuthClaimTypes.ExternalId, subject.Value));

        var email = identity.FindFirst("email");
        if (email is not null && identity.FindFirst(AuthClaimTypes.Email) is null)
            identity.AddClaim(new Claim(AuthClaimTypes.Email, email.Value));

        var name = identity.FindFirst("name");
        if (name is not null && identity.FindFirst(AuthClaimTypes.Name) is null)
            identity.AddClaim(new Claim(AuthClaimTypes.Name, name.Value));

        if (identity.FindFirst(AuthClaimTypes.AuthProvider) is null)
            identity.AddClaim(new Claim(AuthClaimTypes.AuthProvider, AuthSchemes.Google));
    }
}
