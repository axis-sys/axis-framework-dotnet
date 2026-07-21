namespace AxisValidator.Usa.UnitTests;

public class UsaValidatorTests
{
    // ── FormatPhone ─────────────────────────────────────────────────────────

    [Fact]
    public void FormatPhoneReturnsSuccessForValidUsaNumber()
    {
        var result = UsaValidator.FormatPhone("2125551234");

        result.ShouldSucceedWith("(212) 555-1234");
    }

    [Fact]
    public void FormatPhoneReturnsFailureForInvalidNumber()
    {
        var result = UsaValidator.FormatPhone("invalid");

        result.ShouldFailWithCode("PHONE_NUMBER_NULL_OR_NOT_VALID");
    }

    [Fact]
    public void FormatPhoneReturnsFailureForNullNumber()
    {
        var result = UsaValidator.FormatPhone(null);

        result.ShouldFail();
    }

    // ── ValidateSsn ─────────────────────────────────────────────────────────

    [Fact]
    public void ValidateSsnReturnsSuccessForValidUsaSsn()
    {
        var result = UsaValidator.ValidateSsn("512456789");

        result.ShouldSucceedWith("512456789");
    }

    [Fact]
    public void ValidateSsnReturnsFailureForInvalidSsn()
    {
        var result = UsaValidator.ValidateSsn("666123456");

        result.ShouldFailWithCode("DOCUMENT_INVALID");
    }

    [Fact]
    public void ValidateSsnReturnsFailureForNullDocument()
    {
        var result = UsaValidator.ValidateSsn(null);

        result.ShouldFailWithCode("DOCUMENT_INVALID");
    }
}
