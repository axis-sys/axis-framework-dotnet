using Axis;

namespace AxisValidator.Usa;

public static class UsaValidator
{
    public static AxisResult<string> FormatPhone(string? phone)
    {
        var formatted = PhoneValidator.Format(phone);
        if (formatted == null)
            return AxisError.ValidationRule("PHONE_NUMBER_NULL_OR_NOT_VALID");

        return AxisResult.Ok(formatted);
    }

    public static AxisResult<string> ValidateSsn(string? document)
    {
        if (!SsnValidator.Validate(document))
            return AxisError.ValidationRule("DOCUMENT_INVALID");

        return AxisResult.Ok(document!);
    }
}
