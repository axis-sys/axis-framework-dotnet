using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestEmailValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;

namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;

public interface IEmailValidationFacade
{
    Task<AxisResult<RequestEmailValidationResponse>> RequestEmailValidationAsync(RequestEmailValidationCommand command);
    Task<AxisResult<ValidateEmailResponse>> ValidateEmailAsync(ValidateEmailCommand command);
    Task<AxisResult<GetValidationStatusResponse>> GetValidationStatusAsync(GetValidationStatusQuery query);
    Task<AxisResult<CancelValidationResponse>> CancelValidationAsync(CancelValidationCommand command);
}
