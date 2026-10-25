using Scaffolds.ECommerce.Domain.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;

public interface IValidationCodesWritePort
{
    Task<AxisResult<IValidationCodeEntityProperties>> CreateAsync(IValidationCodeEntityProperties validationCode);
    Task<AxisResult<IValidationCodeEntityProperties>> UpdateAsync(IValidationCodeEntityProperties validationCode);
    Task<AxisResult> RemoveAsync(CustomerId customerId);
}
