using Axis.Ports;
using Axis.SharedKernel;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driven.Auth;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Validate;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.Validate.v1;

internal sealed class ValidateEmailHandler(
    IAxisMediator mediator,
    IValidationCodesPort validationCodes,
    IAxisSagaMediator sagaMediator
) : IAxisCommandHandler<ValidateEmailCommand, ValidateEmailResponse>
{
    public Task<AxisResult<ValidateEmailResponse>> HandleAsync(ValidateEmailCommand command)
        => validationCodes.GetAsync(mediator.AxisEntityId)
            .EnsureAsync(stored => stored == command.Code, AxisError.ValidationRule(EmailValidationErrors.CodeMismatch))
            .ThenAsync(_ => sagaMediator.StartAsync(ValidateEmailSaga.Name, new ValidateEmailPayload
            {
                CustomerId = mediator.AxisEntityId!.Value,
            }))
            .MapAsync(sagaId => new ValidateEmailResponse
            {
                SagaId = sagaId,
                Status = nameof(AxisSagaStatus.Pending),
            });
}
