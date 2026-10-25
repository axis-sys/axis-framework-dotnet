using Axis.Contracts;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Sagas.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail.Handlers;

internal sealed class RevertEmailValidatedStageHandler(
    ICustomersFacade customersFacade
) : IAxisSagaStageHandler<ValidateEmailPayload>
{
    public string SagaName => ValidateEmailSaga.Name;
    public string StageName => ValidateEmailSaga.RevertEmailValidatedStage;

    public Task<AxisResult<ValidateEmailPayload>> ExecuteAsync(ValidateEmailPayload payload)
    {
        if (!payload.EmailMarkedValidated || payload.EmailWasAlreadyValidated || payload.EmailValidationReverted)
            return AxisResult.Ok(payload).AsTaskAsync();

        return customersFacade
            .RevertEmailValidatedAsync(new RevertEmailValidatedCommand { CustomerId = payload.CustomerId })
            .MapAsync(_ => payload with { EmailValidationReverted = true });
    }
}
