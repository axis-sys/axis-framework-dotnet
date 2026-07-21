using Scaffolds.ECommerce.Adapters.Driven.Repository.Auth.ValidationCodes;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Auth;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAuthRepositories(this IServiceCollection services)
    {
        services.AddScoped<IValidationCodesPort, ValidationCodesRepository>();
        return services;
    }
}
