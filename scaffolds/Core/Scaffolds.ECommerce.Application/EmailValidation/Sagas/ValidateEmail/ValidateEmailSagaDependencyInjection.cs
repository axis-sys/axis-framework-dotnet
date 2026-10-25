using Axis.Contracts.Configuration;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Sagas.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;

internal static class ValidateEmailSagaDependencyInjection
{
    internal static IServiceCollection AddValidateEmailSaga(this IServiceCollection services)
    {
        services.AddSingleton(AxisSagaDefinitions.Define<ValidateEmailPayload>(ValidateEmailSaga.Name, ValidateEmailSaga.Configure));
        return services;
    }
}
