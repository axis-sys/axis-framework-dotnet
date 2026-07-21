namespace AxisTypes.UnitTests;

public class AxisEntityIdTests
{
    [Fact]
    public void NewProducesValidVersion7Guid()
    {
        var id = AxisEntityId.New;

        Assert.True(Guid.TryParse(id.ToString(), out var guid));
        Assert.Equal(7, guid.Version);
    }

    [Fact]
    public void ImplicitConversionRoundtrips()
    {
        var original = AxisEntityId.New;

        string asString = original;
        AxisEntityId parsed = asString;

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void TryParseAcceptsValidGuid7String()
    {
        var raw = Guid.CreateVersion7().ToString();

        Assert.True(AxisEntityId.TryParse(raw, out var id));
        Assert.Equal(raw, id.ToString());
    }

    [Fact]
    public void TryParseRejectsInvalidInput()
    {
        Assert.False(AxisEntityId.TryParse("not-a-guid", out _));
        Assert.False(AxisEntityId.TryParse(null, out _));
    }

    [Fact]
    public void TryParseRejectsNonVersion7Guid()
    {
        var guidV4 = Guid.NewGuid().ToString();

        Assert.False(AxisEntityId.TryParse(guidV4, out _));
    }

    [Fact]
    public void ImplicitFromNullThrows() => Assert.Throws<ArgumentNullException>(() => { AxisEntityId _ = null!; });

    [Fact]
    public void ImplicitFromNonGuidInputThrows() => Assert.Throws<ArgumentException>(() => { AxisEntityId _ = "not-a-guid"; });

    [Fact]
    public void ImplicitFromNonVersion7GuidThrows()
    {
        var guidV4 = Guid.NewGuid().ToString();

        Assert.Throws<ArgumentException>(() => { AxisEntityId _ = guidV4; });
    }

    [Fact]
    public void EqualityIsCaseInsensitive()
    {
        var guid = Guid.CreateVersion7().ToString();

        AxisEntityId lower = guid.ToLowerInvariant();
        AxisEntityId upper = guid.ToUpperInvariant();

        Assert.Equal(lower, upper);
        Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
    }
}
