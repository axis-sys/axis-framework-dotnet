using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Contracts.Driven.Auth;

namespace Scaffolds.ECommerce.Adapters.Driven.AuthProviders;

internal sealed class HttpExternalIdentityContext(IHttpContextAccessor httpContextAccessor) : IExternalIdentityContext
{
    public AxisResult<ExternalUser> Get()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var subject = user?.FindFirstValue(AuthClaimTypes.ExternalId);
        var provider = user?.FindFirstValue(AuthClaimTypes.AuthProvider);
        if (user is null || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(provider))
            return AxisError.Unauthorized(AuthProviderErrors.ExternalTokenInvalid);

        return new ExternalUser(
            subject,
            user.FindFirstValue(AuthClaimTypes.Email),
            user.Identity?.Name ?? user.FindFirstValue(AuthClaimTypes.Name),
            provider);
    }
}
