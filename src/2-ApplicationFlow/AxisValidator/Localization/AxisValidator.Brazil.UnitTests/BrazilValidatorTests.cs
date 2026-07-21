namespace AxisValidator.Brazil.UnitTests;

public class BrazilValidatorTests
{
    // ── FormatCellphone ─────────────────────────────────────────────────────

    [Fact]
    public void FormatCellphoneReturnsSuccessForValidBrazilianNumber()
    {
        var result = BrazilValidator.FormatCellphone("11987654321");

        result.ShouldSucceedWith("(11) 98765-4321");
    }

    [Fact]
    public void FormatCellphoneReturnsFailureForInvalidNumber()
    {
        var result = BrazilValidator.FormatCellphone("invalid");

        result.ShouldFailWithCode("CELLPHONE_NUMBER_NULL_OR_NOT_VALID");
    }

    [Fact]
    public void FormatCellphoneReturnsFailureForNullNumber()
    {
        var result = BrazilValidator.FormatCellphone(null);

        result.ShouldFail();
    }

    // ── ValidateCpf ─────────────────────────────────────────────────────────

    [Fact]
    public void ValidateCpfReturnsSuccessForValidBrazilianCpf()
    {
        var result = BrazilValidator.ValidateCpf("39053344705");

        result.ShouldSucceedWith("39053344705");
    }

    [Fact]
    public void ValidateCpfReturnsFailureForInvalidCpf()
    {
        var result = BrazilValidator.ValidateCpf("12345678900");

        result.ShouldFailWithCode("DOCUMENT_INVALID");
    }

    [Fact]
    public void ValidateCpfReturnsFailureForNullDocument()
    {
        var result = BrazilValidator.ValidateCpf(null);

        result.ShouldFailWithCode("DOCUMENT_INVALID");
    }
}
