using Axis.Contracts;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail.Handlers;

internal sealed class RevertEmailValidatedStageHandler(
    ICustomersFacade customersFacade,
    IUnitOfWork unitOfWork
) : IAxisSagaStageHandler<ValidateEmailPayload>
{
    public string SagaName => ValidateEmailSaga.Name;
    public string StageName => nameof(RevertEmailValidatedStageHandler);

    public Task<AxisResult<ValidateEmailPayload>> ExecuteAsync(ValidateEmailPayload payload)
    {
        // Undo only what this run actually changed: a mark that never landed, or an email that was
        // already validated before the run, must not be reverted.
        if (!payload.EmailMarkedValidated || payload.EmailWasAlreadyValidated || payload.EmailValidationReverted)
            return AxisResult.Ok(payload).AsTaskAsync();

        return customersFacade
            .RevertEmailValidatedAsync(new RevertEmailValidatedCommand { CustomerId = payload.CustomerId })
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .MapAsync(_ => payload with { EmailValidationReverted = true });
    }
}
