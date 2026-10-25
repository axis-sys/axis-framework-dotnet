using Axis.Ports;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.CancelValidation.v1;

internal sealed class CancelValidationHandler(IAxisSagaMediator sagaMediator)
    : IAxisCommandHandler<CancelValidationCommand, CancelValidationResponse>
{
    public Task<AxisResult<CancelValidationResponse>> HandleAsync(CancelValidationCommand command)
        => ValidateEmailRuns.FindAsync(sagaMediator, command.SagaId!)
            .ThenAsync(run => sagaMediator.CancelAsync(run.SagaId).MapErrorAsync(ValidateEmailRuns.RunErrorOf))
            .MapAsync(run => new CancelValidationResponse
            {
                SagaId = run.SagaId,
                Status = run.Status.ToString(),
            });
}
