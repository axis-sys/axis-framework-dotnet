using Scaffolds.ECommerce.Domain.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;

public interface IValidationCodesReaderPort
{
    Task<AxisResult<IValidationCodeEntityProperties>> GetByCustomerIdAsync(CustomerId customerId);
}
