namespace AxisValidator.Usa.UnitTests;

public class PhoneValidatorTests
{
    // ── Format ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2125551234", "(212) 555-1234")]
    [InlineData("12125551234", "(212) 555-1234")]
    [InlineData("+1 (212) 555-1234", "(212) 555-1234")]
    [InlineData("212-555-1234", "(212) 555-1234")]
    public void FormatProducesExpectedUsaFormat(string input, string expected)
    {
        Assert.Equal(expected, PhoneValidator.Format(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcde")]
    [InlineData("212a5551234")]
    [InlineData("0125551234")]
    [InlineData("1125551234")]
    [InlineData("12345")]
    [InlineData("123456")]
    public void FormatReturnsNullForInvalidInputs(string? input)
    {
        Assert.Null(PhoneValidator.Format(input));
    }

    // ── TryFormat ───────────────────────────────────────────────────────────

    [Fact]
    public void TryFormatReturnsTrueForValidPhone()
    {
        var ok = PhoneValidator.TryFormat("2125551234", out var formatted);

        Assert.True(ok);
        Assert.Equal("(212) 555-1234", formatted);
    }

    [Fact]
    public void TryFormatReturnsFalseForInvalidPhone()
    {
        var ok = PhoneValidator.TryFormat("invalid", out var formatted);

        Assert.False(ok);
        Assert.Null(formatted);
    }

    [Fact]
    public void TryFormatReturnsFalseForNull()
    {
        var ok = PhoneValidator.TryFormat(null, out var formatted);

        Assert.False(ok);
        Assert.Null(formatted);
    }

    // ── OnlyNumbers ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2125551234", "2125551234")]
    [InlineData("12125551234", "2125551234")]
    [InlineData("(212) 555-1234", "2125551234")]
    [InlineData("212-555-1234", "2125551234")]
    public void OnlyNumbersStripsNonDigitsAndNormalizes(string input, string expected)
    {
        Assert.Equal(expected, PhoneValidator.OnlyNumbers(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("2125551234x")]
    public void OnlyNumbersReturnsNullForEmptyOrLetters(string? input)
    {
        Assert.Null(PhoneValidator.OnlyNumbers(input));
    }

    [Fact]
    public void OnlyNumbersReturnsNullForNineDigits()
    {
        Assert.Null(PhoneValidator.OnlyNumbers("212555123"));
    }

    [Fact]
    public void OnlyNumbersReturnsNullForElevenDigitsNotStartingWithCountryCode()
    {
        Assert.Null(PhoneValidator.OnlyNumbers("32125551234"));
    }

    [Fact]
    public void OnlyNumbersReturnsNullForTwelvePlusDigits()
    {
        Assert.Null(PhoneValidator.OnlyNumbers("123456789012"));
    }

    [Fact]
    public void FormatReturnsNullWhenAreaCodeStartsWithZeroOrOne()
    {
        Assert.Null(PhoneValidator.Format("0125551234"));
        Assert.Null(PhoneValidator.Format("1125551234"));
    }

    [Fact]
    public void FormatReturnsNullWhenExchangeCodeStartsWithZeroOrOne()
    {
        Assert.Null(PhoneValidator.Format("2120551234"));
        Assert.Null(PhoneValidator.Format("2121551234"));
    }
}
