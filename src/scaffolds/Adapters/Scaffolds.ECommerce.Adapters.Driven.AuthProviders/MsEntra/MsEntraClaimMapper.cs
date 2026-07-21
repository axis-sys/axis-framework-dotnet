using System.Security.Claims;
using Scaffolds.ECommerce.Application.Auth;

namespace Scaffolds.ECommerce.Adapters.Driven.AuthProviders.MsEntra;

// Normalizes Microsoft Entra claims into the app's own claim names, so everything after
// authentication (the IExternalIdentityContext port included) is provider-agnostic.
internal static class MsEntraClaimMapper
{
    private const string ObjectIdentifierClaimUrl = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public static void MapCustomClaims(ClaimsIdentity identity)
    {
        var oid = identity.FindFirst("oid") ?? identity.FindFirst(ObjectIdentifierClaimUrl);
        if (oid is not null && identity.FindFirst(AuthClaimTypes.ExternalId) is null)
            identity.AddClaim(new Claim(AuthClaimTypes.ExternalId, oid.Value));

        var email = identity.FindFirst("emails")
                    ?? identity.FindFirst("email")
                    ?? identity.FindFirst("preferred_username")
                    ?? identity.FindFirst("upn");
        if (email is not null && identity.FindFirst(AuthClaimTypes.Email) is null)
            identity.AddClaim(new Claim(AuthClaimTypes.Email, email.Value));

        var name = identity.FindFirst("name");
        if (name is not null && identity.FindFirst(AuthClaimTypes.Name) is null)
            identity.AddClaim(new Claim(AuthClaimTypes.Name, name.Value));

        if (identity.FindFirst(AuthClaimTypes.AuthProvider) is null)
            identity.AddClaim(new Claim(AuthClaimTypes.AuthProvider, AuthSchemes.MsEntra));
    }
}
