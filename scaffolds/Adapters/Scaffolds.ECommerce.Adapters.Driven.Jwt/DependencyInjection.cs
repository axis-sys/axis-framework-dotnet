using Scaffolds.ECommerce.Adapters.Driven.Jwt.Auth;
using Scaffolds.ECommerce.Contracts.Driven.Auth;

namespace Scaffolds.ECommerce.Adapters.Driven.Jwt;

public static class DependencyInjection
{
    public static IServiceCollection AddJwtAccessTokens(this IServiceCollection services)
    {
        services.AddScoped<IAccessTokenSignerPort, AccessTokenSigner>();
        return services;
    }
}
