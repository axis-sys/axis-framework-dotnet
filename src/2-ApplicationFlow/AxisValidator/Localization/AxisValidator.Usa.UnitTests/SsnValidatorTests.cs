namespace AxisValidator.Usa.UnitTests;

public class SsnValidatorTests
{
    [Theory]
    [InlineData("512456789")]
    [InlineData("456789012")]
    [InlineData("212345678")]
    [InlineData("512-45-6789")]
    public void ValidateReturnsTrueForValidSsn(string ssn)
    {
        Assert.True(SsnValidator.Validate(ssn));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("abcdefghi")]
    [InlineData("000000000")]
    [InlineData("123456789")]
    [InlineData("666123456")]
    [InlineData("900123456")]
    [InlineData("123006789")]
    [InlineData("123450000")]
    [InlineData("078051120")]
    public void ValidateReturnsFalseForInvalidSsn(string? ssn)
    {
        Assert.False(SsnValidator.Validate(ssn));
    }
}
