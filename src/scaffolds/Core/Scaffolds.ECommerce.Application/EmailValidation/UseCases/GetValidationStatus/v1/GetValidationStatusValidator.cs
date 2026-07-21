using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.GetValidationStatus.v1;

internal sealed class GetValidationStatusValidator : AxisValidatorBase<GetValidationStatusQuery>
{
    public GetValidationStatusValidator()
    {
        RequiredGuid7(x => x.SagaId, EmailValidationErrors.SagaIdInvalid);
    }
}
