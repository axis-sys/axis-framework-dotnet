using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Validate;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.Validate.v1;

internal sealed class ValidateEmailValidator : AxisValidatorBase<ValidateEmailCommand>
{
    public ValidateEmailValidator()
    {
        RequiredWithMaxLength(x => x.Code, EmailValidationErrors.CodeRequired);
    }
}
