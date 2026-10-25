using Scaffolds.ECommerce.Application.EmailValidation.Aggregates;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation;

internal static class DependencyInjection
{
    internal static IServiceCollection AddEmailValidationServices(this IServiceCollection services)
    {
        services.AddScoped<IValidationCodeFactory, ValidationCodeFactory>();
        services.AddValidateEmailSaga();
        return services;
    }
}
