namespace Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;

public static class ValidationCodeErrors
{
    public static readonly AxisError ValidationCodeAlreadyExists = AxisError.Conflict("VALIDATION_CODE_ALREADY_EXISTS");

    public const string CodeRequired = "CODE_REQUIRED";
    public static readonly AxisError ValidationCodeNotFound = AxisError.NotFound("EMAIL_VALIDATION_CODE_NOT_FOUND");
    public static readonly AxisError CodeMismatch = AxisError.ValidationRule("EMAIL_VALIDATION_CODE_MISMATCH");

    public const string SagaIdInvalid = "SAGA_ID_INVALID";
}
