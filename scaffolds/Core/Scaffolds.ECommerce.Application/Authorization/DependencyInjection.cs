using Scaffolds.ECommerce.Application.Authorization.Aggregates;
using Scaffolds.ECommerce.Application.Authorization.Services.GrantsCache;

namespace Scaffolds.ECommerce.Application.Authorization;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAuthorizationServices(this IServiceCollection services)
    {
        services.AddScoped<IMemberFactory, MemberFactory>();
        services.AddScoped<IGrantsCacheService, GrantsCacheService>();
        return services;
    }
}
