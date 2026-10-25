using Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation;

internal static class DependencyInjection
{
    internal static IServiceCollection AddEmailValidationRepositories(this IServiceCollection services)
    {
        services.AddScoped<ValidationCodesRepository>();
        services.AddScoped<IValidationCodesReaderPort>(provider => provider.GetRequiredService<ValidationCodesRepository>());
        services.AddScoped<IValidationCodesWritePort>(provider => provider.GetRequiredService<ValidationCodesRepository>());
        return services;
    }
}
