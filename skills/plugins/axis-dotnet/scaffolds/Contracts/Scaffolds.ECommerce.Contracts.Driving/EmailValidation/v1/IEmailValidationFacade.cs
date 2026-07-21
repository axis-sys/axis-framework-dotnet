using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestCode;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Validate;

namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;

public interface IEmailValidationFacade
{
    Task<AxisResult<RequestEmailValidationResponse>> RequestCodeAsync(RequestEmailValidationCommand command);
    Task<AxisResult<ValidateEmailResponse>> ValidateAsync(ValidateEmailCommand command);
    Task<AxisResult<GetValidationStatusResponse>> GetValidationStatusAsync(GetValidationStatusQuery query);
}
