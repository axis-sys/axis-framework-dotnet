using Axis.Contracts;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail.Handlers;

internal sealed class MarkEmailValidatedStageHandler(
    ICustomersFacade customersFacade
) : IAxisSagaStageHandler<ValidateEmailPayload>
{
    public string SagaName => ValidateEmailSaga.Name;
    public string StageName => nameof(MarkEmailValidatedStageHandler);

    // No unitOfWork here: MarkEmailValidatedAsync (Customers BC, across the facade boundary) already
    // commits its own write when it has one — a second SaveChangesAsync on the same scoped unit of work
    // has no pending write and errors, including on the AlreadyValidated branch, which writes nothing at all.
    public Task<AxisResult<ValidateEmailPayload>> ExecuteAsync(ValidateEmailPayload payload)
    {
        // Stage-level idempotency for saga resume: a mark that already landed in a prior run must not
        // re-invoke the facade (mirrors RevertEmailValidatedStageHandler's own resume guard).
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
