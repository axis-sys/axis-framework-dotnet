using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Domain.EmailValidation.ValidationCodes;

internal sealed record ValidationCodeProperties(
    CustomerId CustomerId,
    string Code
) : IValidationCodeEntityProperties;
