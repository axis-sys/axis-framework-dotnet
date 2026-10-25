using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Sagas.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;

internal static class ValidateEmailPayloadFactory
{
    public static ValidateEmailPayload Create(CustomerId customerId)
        => new() { CustomerId = customerId };
}
