namespace Scaffolds.ECommerce.Application.EmailValidation;

internal static class EmailValidationErrors
{
    // GetValidationStatus
    public const string SagaIdInvalid = "SAGA_ID_INVALID";

    // Validate
    public const string CodeRequired = "CODE_REQUIRED";
    public const string CodeMismatch = "EMAIL_VALIDATION_CODE_MISMATCH";
}
