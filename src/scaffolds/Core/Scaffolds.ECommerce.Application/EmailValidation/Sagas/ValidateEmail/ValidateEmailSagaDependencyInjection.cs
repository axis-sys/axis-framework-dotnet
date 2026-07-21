using Axis.Contracts.Configuration;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;

internal static class ValidateEmailSagaDependencyInjection
{
    internal static IServiceCollection AddValidateEmailSaga(this IServiceCollection services)
    {
        services.AddSingleton(AxisSagaDefinitions.Define<ValidateEmailPayload>(ValidateEmailSaga.Name, ValidateEmailSaga.Configure));
        return services;
    }
}
