using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestEmailValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade.EmailValidation;

internal sealed class EmailValidationFacade(IAxisMediator mediator) : IEmailValidationFacade
{
    public Task<AxisResult<RequestEmailValidationResponse>> RequestEmailValidationAsync(RequestEmailValidationCommand command)
        => mediator.Cqrs.ExecuteAsync<RequestEmailValidationCommand, RequestEmailValidationResponse>(command);

    public Task<AxisResult<ValidateEmailResponse>> ValidateEmailAsync(ValidateEmailCommand command)
        => mediator.Cqrs.ExecuteAsync<ValidateEmailCommand, ValidateEmailResponse>(command);

    public Task<AxisResult<GetValidationStatusResponse>> GetValidationStatusAsync(GetValidationStatusQuery query)
        => mediator.Cqrs.QueryAsync<GetValidationStatusQuery, GetValidationStatusResponse>(query);

    public Task<AxisResult<CancelValidationResponse>> CancelValidationAsync(CancelValidationCommand command)
        => mediator.Cqrs.ExecuteAsync<CancelValidationCommand, CancelValidationResponse>(command);
}
