using Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.CancelValidation.v1;

internal sealed class CancelValidationValidator : AxisValidatorBase<CancelValidationCommand>
{
    public CancelValidationValidator()
    {
        RequiredGuid7(x => x.SagaId, ValidationCodeErrors.SagaIdInvalid);
    }
}
