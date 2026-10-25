using Axis.Ports;
using Axis.SharedKernel;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.ValidateEmail.v1;

internal sealed class ValidateEmailHandler(
    IAxisMediator mediator,
    IValidationCodesReaderPort validationCodes,
    IAxisSagaMediator sagaMediator
) : IAxisCommandHandler<ValidateEmailCommand, ValidateEmailResponse>
{
    public Task<AxisResult<ValidateEmailResponse>> HandleAsync(ValidateEmailCommand command)
    {
        var callerId = mediator.AxisEntityId is { } caller ? (CustomerId?)caller.ToString() : null;

        return callerId.Rop()
            .Ensure(customerId => customerId.HasValue, SharedErrors.CustomerIdentityRequired)
            .ThenAsync(customerId => validationCodes.GetByCustomerIdAsync(customerId!.Value))
            .EnsureAsync(stored => stored.Code == command.Code, ValidationCodeErrors.CodeMismatch)
            .ThenAsync(stored => sagaMediator
                .StartAsync(ValidateEmailSaga.Name, ValidateEmailPayloadFactory.Create(stored.CustomerId))
                .MapErrorAsync(ValidateEmailRuns.RunErrorOf))
            .MapAsync(sagaId => new ValidateEmailResponse
            {
                SagaId = sagaId,
                Status = nameof(AxisSagaStatus.Pending),
            });
    }
}
