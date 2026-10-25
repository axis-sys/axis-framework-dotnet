using Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.ValidateEmail.v1;

internal sealed class ValidateEmailValidator : AxisValidatorBase<ValidateEmailCommand>
{
    public ValidateEmailValidator()
    {
        RequiredWithMaxLength(x => x.Code, ValidationCodeErrors.CodeRequired);
    }
}
