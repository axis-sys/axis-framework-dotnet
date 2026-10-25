using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Domain.EmailValidation.ValidationCodes;

public interface IValidationCodeEntityProperties
{
    CustomerId CustomerId { get; }
    string Code { get; }
}
