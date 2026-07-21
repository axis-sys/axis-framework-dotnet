using Axis.Contracts.Configuration;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail.Handlers;

namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;

internal static class ValidateEmailSaga
{
    public const string Name = "ValidateEmail";
    
    public static void Configure(IAxisSagaConfigurator<ValidateEmailPayload> saga)
    {
        saga.AddStage(nameof(MarkEmailValidatedStageHandler))
            .NextStageOnSuccess(nameof(RemoveValidationCodeStageHandler));

        // Removing the consumed code is the terminal forward stage; if it fails for good, the mark in
        // the Customers BC is undone by the compensation stage, so the run ends Compensated, not half-done.
        saga.AddStage(nameof(RemoveValidationCodeStageHandler))
            .FinishOnSuccess()
            .RouteToOnError(nameof(RevertEmailValidatedStageHandler));

        saga.AddErrorStage(nameof(RevertEmailValidatedStageHandler)).FinishOnSuccess();
    }
}
