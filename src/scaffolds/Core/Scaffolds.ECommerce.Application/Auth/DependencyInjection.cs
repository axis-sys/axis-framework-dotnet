using Scaffolds.ECommerce.Application.Auth.Services.AuthTokenIssuer;

namespace Scaffolds.ECommerce.Application.Auth;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthTokenIssuerService, AuthTokenIssuerService>();
        return services;
    }
}
