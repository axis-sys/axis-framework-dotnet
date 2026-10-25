using Scaffolds.ECommerce.SharedKernel.Auth;

namespace Scaffolds.ECommerce.Host.Authorization;

internal static class DependencyInjection
{
    internal static IServiceCollection AddECommerceAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthorizationPolicies.Administrator, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new AxisErrorRequirement(
                    AuthorizationErrors.AdministratorRequired,
                    user => user.HasClaim(AuthClaimTypes.Role, AuthRoles.Administrator))));

        services.AddAxisAuthorizationProblemDetails();
        services.AddScoped<PermissionAuthorizationFilter>();
        services.Configure<MvcOptions>(mvc => mvc.Filters.AddService<PermissionAuthorizationFilter>());

        return services;
    }
}
