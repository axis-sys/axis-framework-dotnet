using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation;

internal static class DependencyInjection
{
    internal static IServiceCollection AddEmailValidationServices(this IServiceCollection services)
    {
        services.AddValidateEmailSaga();
        return services;
    }
}
