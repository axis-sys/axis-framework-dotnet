using AxisValidator.Brazil.Helpers;

namespace AxisValidator.Brazil.UnitTests;

public class RandomBrazilianDataHelperTests
{
    [Fact]
    public void GenerateCpfProduces11DigitUnformattedString()
    {
        var cpf = RandomBrazilianDataHelper.GenerateCpf();

        Assert.Equal(11, cpf.Length);
        Assert.All(cpf, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void GenerateCpfProducesFormattedString()
    {
        var cpf = RandomBrazilianDataHelper.GenerateCpf(format: true);

        Assert.Equal(14, cpf.Length);
        Assert.Equal('.', cpf[3]);
        Assert.Equal('.', cpf[7]);
        Assert.Equal('-', cpf[11]);
    }

    [Fact]
    public void GenerateCpfProducesCpfThatPassesCpfValidator()
    {
        for (var i = 0; i < 10; i++)
        {
            var cpf = RandomBrazilianDataHelper.GenerateCpf();
            Assert.True(CpfValidator.Validate(cpf), $"Generated CPF {cpf} should be valid");
        }
    }
}
