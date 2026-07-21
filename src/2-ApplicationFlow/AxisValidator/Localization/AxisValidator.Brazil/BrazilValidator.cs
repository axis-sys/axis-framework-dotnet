using Axis;

namespace AxisValidator.Brazil;

public static class BrazilValidator
{
    public static AxisResult<string> FormatCellphone(string? phone)
    {
        var formatted = CellphoneValidator.Format(phone);
        if (formatted == null)
            return AxisError.ValidationRule("CELLPHONE_NUMBER_NULL_OR_NOT_VALID");

        return AxisResult.Ok(formatted);
    }

    public static AxisResult<string> ValidateCpf(string? document)
    {
        if (!CpfValidator.Validate(document))
            return AxisError.ValidationRule("DOCUMENT_INVALID");

        return AxisResult.Ok(document!);
    }
}
