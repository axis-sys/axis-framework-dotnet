using Axis.Contracts;
using Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Sagas.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail.Handlers;

internal sealed class RemoveValidationCodeStageHandler(
    IValidationCodesWritePort validationCodes,
    IUnitOfWork unitOfWork
) : IAxisSagaStageHandler<ValidateEmailPayload>
{
    public string SagaName => ValidateEmailSaga.Name;
    public string StageName => ValidateEmailSaga.RemoveValidationCodeStage;

    public Task<AxisResult<ValidateEmailPayload>> ExecuteAsync(ValidateEmailPayload payload)
    {
        var customerId = (CustomerId)payload.CustomerId;

        if (payload.CodeRemoved)
            return AxisResult.Ok(payload).AsTaskAsync();

        return validationCodes.RemoveAsync(customerId)
            .ThenAsync(unitOfWork.SaveChangesAsync)
            .WithValueAsync(payload with { CodeRemoved = true });
    }
}
