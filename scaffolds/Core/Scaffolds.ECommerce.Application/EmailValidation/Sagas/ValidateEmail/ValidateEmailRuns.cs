using Axis.Ports;
using Axis.SharedKernel;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;

internal static class ValidateEmailRuns
{
    public static Task<AxisResult<AxisSagaInstance>> FindAsync(IAxisSagaMediator sagaMediator, string sagaId)
        => sagaMediator.GetByIdAsync(sagaId)
            .MapErrorAsync(RunErrorOf)
            .EnsureAsync(run => run.SagaName == ValidateEmailSaga.Name, SharedErrors.SagaRunNotFound);

    public static AxisError RunErrorOf(AxisError error) => error.Code switch
    {
        AxisSagaErrors.SagaInstanceNotFound => SharedErrors.SagaRunNotFound,
        AxisSagaErrors.NotCancellable => SharedErrors.SagaRunNotCancellable,
        AxisSagaErrors.PersistenceFailed => SharedErrors.SagaRunStoreFailed,
        _ => SharedErrors.SagaRunFailed,
    };
}
