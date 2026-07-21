using Axis.Contracts;
using Scaffolds.ECommerce.Contracts.Driven.Auth;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail.Handlers;

internal sealed class RemoveValidationCodeStageHandler(
    IValidationCodesPort validationCodes,
    IUnitOfWork unitOfWork
) : IAxisSagaStageHandler<ValidateEmailPayload>
{
    public string SagaName => ValidateEmailSaga.Name;
    public string StageName => nameof(RemoveValidationCodeStageHandler);

    // ValidationCodes is a real repository now — the delete must be committed.
    public Task<AxisResult<ValidateEmailPayload>> ExecuteAsync(ValidateEmailPayload payload)
    {
        if (payload.CodeRemoved)
            return AxisResult.Ok(payload).AsTaskAsync();

        return validationCodes.RemoveAsync(payload.CustomerId)
            .ThenAsync(unitOfWork.SaveChangesAsync)
            .WithValueAsync(payload with { CodeRemoved = true });
    }
}
