using Axis.Contracts.Configuration;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail.Handlers;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Sagas.ValidateEmail;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;

internal static class ValidateEmailSaga
{
    public const string Name = "ValidateEmail";
    public const string MarkEmailValidatedStage = nameof(MarkEmailValidatedStageHandler);
    public const string RemoveValidationCodeStage = nameof(RemoveValidationCodeStageHandler);
    public const string RevertEmailValidatedStage = nameof(RevertEmailValidatedStageHandler);

    public static void Configure(IAxisSagaConfigurator<ValidateEmailPayload> saga)
    {
        saga.AddStage(MarkEmailValidatedStage)
            .NextStageOnSuccess(RemoveValidationCodeStage);

        saga.AddStage(RemoveValidationCodeStage)
            .FinishOnSuccess()
            .RouteToOnError(RevertEmailValidatedStage);

        saga.AddErrorStage(RevertEmailValidatedStage).FinishOnSuccess();
    }
}
