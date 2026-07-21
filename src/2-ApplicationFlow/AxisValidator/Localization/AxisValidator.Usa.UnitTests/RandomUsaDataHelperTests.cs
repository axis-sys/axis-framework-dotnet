using AxisValidator.Usa.Helpers;

namespace AxisValidator.Usa.UnitTests;

public class RandomUsaDataHelperTests
{
    [Fact]
    public void GenerateSsnProduces9DigitUnformattedString()
    {
        var ssn = RandomUsaDataHelper.GenerateSsn();

        Assert.Equal(9, ssn.Length);
        Assert.All(ssn, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void GenerateSsnProducesFormattedString()
    {
        var ssn = RandomUsaDataHelper.GenerateSsn(format: true);

        Assert.Equal(11, ssn.Length);
        Assert.Equal('-', ssn[3]);
        Assert.Equal('-', ssn[6]);
    }

    [Fact]
    public void GenerateSsnProducesSsnThatPassesSsnValidator()
    {
        for (var i = 0; i < 10; i++)
        {
            var ssn = RandomUsaDataHelper.GenerateSsn();
            Assert.True(SsnValidator.Validate(ssn), $"Generated SSN {ssn} should be valid");
        }
    }
}
