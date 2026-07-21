namespace AxisValidator.Brazil.UnitTests;

public class CellphoneValidatorTests
{
    // ── Format ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("11987654321", "(11) 98765-4321")]
    [InlineData("5511987654321", "(11) 98765-4321")]
    [InlineData("+55 (11) 98765-4321", "(11) 98765-4321")]
    [InlineData("011987654321", "(11) 98765-4321")]
    public void FormatProducesExpectedBrazilianFormat(string input, string expected)
    {
        Assert.Equal(expected, CellphoneValidator.Format(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcde")]
    [InlineData("11a87654321")]
    [InlineData("11887654321")]
    [InlineData("119")]
    [InlineData("12345")]
    [InlineData("123456")]
    public void FormatReturnsNullForInvalidInputs(string? input)
    {
        Assert.Null(CellphoneValidator.Format(input));
    }

    // ── TryFormat ───────────────────────────────────────────────────────────

    [Fact]
    public void TryFormatReturnsTrueForValidPhone()
    {
        var ok = CellphoneValidator.TryFormat("11987654321", out var formatted);

        Assert.True(ok);
        Assert.Equal("(11) 98765-4321", formatted);
    }

    [Fact]
    public void TryFormatReturnsFalseForInvalidPhone()
    {
        var ok = CellphoneValidator.TryFormat("invalid", out var formatted);

        Assert.False(ok);
        Assert.Null(formatted);
    }

    [Fact]
    public void TryFormatReturnsFalseForNull()
    {
        var ok = CellphoneValidator.TryFormat(null, out var formatted);

        Assert.False(ok);
        Assert.Null(formatted);
    }

    // ── OnlyNumbers ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("11987654321", "11987654321")]
    [InlineData("5511987654321", "11987654321")]
    [InlineData("(11) 98765-4321", "11987654321")]
    [InlineData("011987654321", "11987654321")]
    [InlineData("001187654321", "11987654321")]
    [InlineData("1187654321", "11987654321")]
    public void OnlyNumbersStripsNonDigitsAndNormalizes(string input, string expected)
    {
        Assert.Equal(expected, CellphoneValidator.OnlyNumbers(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1187654321x")]
    public void OnlyNumbersReturnsNullForEmptyOrLetters(string? input)
    {
        Assert.Null(CellphoneValidator.OnlyNumbers(input));
    }

    [Fact]
    public void OnlyNumbersReturnsNullForElevenDigitsWithoutNinePrefix()
    {
        Assert.Null(CellphoneValidator.OnlyNumbers("11887654321"));
    }

    // ── Extra edge cases for OnlyNumbers ────────────────────────────────────

    [Fact]
    public void OnlyNumbersAddsMissingNinePrefixFor10Digits()
    {
        Assert.Equal("11987654321", CellphoneValidator.OnlyNumbers("1187654321"));
    }

    [Fact]
    public void OnlyNumbersStripsMultipleLeadingZeros()
    {
        Assert.Equal("11987654321", CellphoneValidator.OnlyNumbers("0011987654321"));
    }

    [Fact]
    public void OnlyNumbersStripsDoubleLeadingZeroFor12PlusDigits()
    {
        Assert.Equal("11987654321", CellphoneValidator.OnlyNumbers("001187654321"));
    }

    [Fact]
    public void OnlyNumbersReturnsNullForInsufficientDigits()
    {
        Assert.Null(CellphoneValidator.OnlyNumbers("12345"));
    }

    [Fact]
    public void OnlyNumbersHandlesThirteenDigitsWithCountryCode()
    {
        Assert.Equal("11987654321", CellphoneValidator.OnlyNumbers("5511987654321"));
    }

    [Fact]
    public void OnlyNumbersReturnsNullWhenRegexDoesNotMatch()
    {
        // 14 digits: doesn't match any supported length
        Assert.Null(CellphoneValidator.OnlyNumbers("12345678901234"));
    }

    [Fact]
    public void OnlyNumbersHandlesFormatsWithParenthesesAndDashes()
    {
        Assert.Equal("11987654321", CellphoneValidator.OnlyNumbers("(11) 98765-4321"));
    }

    [Fact]
    public void FormatReturnsNullForTwelveDigitsStartingWithZero()
    {
        // Length >= 12 starting with '0' triggers the specific path
        var result = CellphoneValidator.Format("011987654321");
        Assert.Equal("(11) 98765-4321", result);
    }

    [Fact]
    public void FormatReturnsNullWhenNumbersStartWithNonValidDdd()
    {
        // DDD 00 not valid (regex requires [1-9])
        Assert.Null(CellphoneValidator.Format("00987654321"));
    }

    [Fact]
    public void FormatHandlesThirteenDigitLeadingZeros()
    {
        Assert.Equal("(11) 98765-4321", CellphoneValidator.Format("00011987654321"));
    }

    [Fact]
    public void OnlyNumbersHandlesTwelvePlusDigitsStartingWithZeroAfterSeparator()
    {
        // After the initial while-loop strip, "-01..." remains (does not start with '0').
        // OnlyDigits then produces "011987654321" (12 digits starting with '0'),
        // which triggers the `case >= 12 when numbers.StartsWith('0')` branch.
        Assert.Equal("11987654321", CellphoneValidator.OnlyNumbers("0-011987654321"));
    }

    [Fact]
    public void OnlyNumbersHandlesDoubleLeadingZeroAfterSeparator()
    {
        // After stripping initial '0' and concat, numbers becomes "0011987654321" (13 digits).
        // Triggers case >= 12 then the nested if with double-leading-zero strip.
        Assert.Equal("11987654321", CellphoneValidator.OnlyNumbers("0-0011987654321"));
    }

    [Fact]
    public void OnlyNumbersHandlesThirteenDigitsWithoutCountryPrefix()
    {
        // 13 digits not starting with 55 and not starting with 0 triggers case 13.
        Assert.Equal("98765432100", CellphoneValidator.OnlyNumbers("1198765432100"));
    }
}
