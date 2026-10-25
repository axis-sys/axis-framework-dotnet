using Axis.Ports;
using Axis.SharedKernel;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.GetValidationStatus.v1;

internal sealed class GetValidationStatusHandler(IAxisSagaMediator sagaMediator)
    : IAxisQueryHandler<GetValidationStatusQuery, GetValidationStatusResponse>
{
    public Task<AxisResult<GetValidationStatusResponse>> HandleAsync(GetValidationStatusQuery query)
        => ValidateEmailRuns.FindAsync(sagaMediator, query.SagaId!)
            .MapAsync(run => new GetValidationStatusResponse
            {
                SagaId = run.SagaId,
                Status = run.Status.ToString(),
                EmailValidated = run.Status == AxisSagaStatus.Completed,
                ErrorCode = run.LastErrorCode,
                ErrorMessage = run.LastErrorMessage,
            });
}
