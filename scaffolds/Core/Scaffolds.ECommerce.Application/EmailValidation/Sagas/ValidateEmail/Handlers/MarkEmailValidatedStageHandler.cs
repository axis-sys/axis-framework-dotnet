using Axis.Contracts;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Sagas.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail.Handlers;

internal sealed class MarkEmailValidatedStageHandler(
    ICustomersFacade customersFacade
) : IAxisSagaStageHandler<ValidateEmailPayload>
{
    public string SagaName => ValidateEmailSaga.Name;
    public string StageName => ValidateEmailSaga.MarkEmailValidatedStage;

    public Task<AxisResult<ValidateEmailPayload>> ExecuteAsync(ValidateEmailPayload payload)
    {
        if (payload.EmailMarkedValidated)
            return payload.Rop().AsTaskAsync();

        return customersFacade
            .MarkEmailValidatedAsync(new MarkEmailValidatedCommand { CustomerId = payload.CustomerId })
            .MapAsync(marked => payload with
            {
                EmailMarkedValidated = true,
                EmailWasAlreadyValidated = marked.AlreadyValidated,
            });
    }
}
