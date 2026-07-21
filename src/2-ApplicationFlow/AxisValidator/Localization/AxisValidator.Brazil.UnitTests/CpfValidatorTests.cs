namespace AxisValidator.Brazil.UnitTests;

public class CpfValidatorTests
{
    [Theory]
    [InlineData("39053344705")]
    [InlineData("11144477735")]
    [InlineData("52998224725")]
    [InlineData("71428793860")]
    [InlineData("15350946056")]
    [InlineData("390.533.447-05")]
    public void ValidateReturnsTrueForValidCpf(string cpf)
    {
        Assert.True(CpfValidator.Validate(cpf));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678900")]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("short")]
    [InlineData("1234567890")]
    [InlineData("abcdefghijk")]
    public void ValidateReturnsFalseForInvalidCpf(string? cpf)
    {
        Assert.False(CpfValidator.Validate(cpf));
    }
}
