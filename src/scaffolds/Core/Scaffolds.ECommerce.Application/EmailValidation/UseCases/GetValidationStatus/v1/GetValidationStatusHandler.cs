using Axis.Ports;
using Axis.SharedKernel;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.GetValidationStatus.v1;

internal sealed class GetValidationStatusHandler(IAxisSagaMediator sagaMediator)
    : IAxisQueryHandler<GetValidationStatusQuery, GetValidationStatusResponse>
{
    public Task<AxisResult<GetValidationStatusResponse>> HandleAsync(GetValidationStatusQuery query)
        => sagaMediator.GetByIdAsync<ValidateEmailPayload>(query.SagaId!)
            .MapAsync(instance => new GetValidationStatusResponse
            {
                SagaId = instance.SagaId,
                Status = instance.Status.ToString(),
                EmailValidated = instance.Status == AxisSagaStatus.Completed,
                ErrorCode = instance.LastErrorCode,
                ErrorMessage = instance.LastErrorMessage,
            });
}
