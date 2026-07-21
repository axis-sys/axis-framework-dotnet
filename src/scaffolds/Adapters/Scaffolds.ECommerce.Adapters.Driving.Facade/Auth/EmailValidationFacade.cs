using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestCode;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Validate;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade.Auth;

internal sealed class EmailValidationFacade(IAxisMediator mediator) : IEmailValidationFacade
{
    public Task<AxisResult<RequestEmailValidationResponse>> RequestCodeAsync(RequestEmailValidationCommand command)
        => mediator.Cqrs.ExecuteAsync<RequestEmailValidationCommand, RequestEmailValidationResponse>(command);

    public Task<AxisResult<ValidateEmailResponse>> ValidateAsync(ValidateEmailCommand command)
        => mediator.Cqrs.ExecuteAsync<ValidateEmailCommand, ValidateEmailResponse>(command);

    public Task<AxisResult<GetValidationStatusResponse>> GetValidationStatusAsync(GetValidationStatusQuery query)
        => mediator.Cqrs.QueryAsync<GetValidationStatusQuery, GetValidationStatusResponse>(query);
}
