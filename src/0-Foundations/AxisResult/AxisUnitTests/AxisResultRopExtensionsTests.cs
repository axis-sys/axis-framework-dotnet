namespace AxisUnitTests;

public class AxisResultRopExtensionsTests
{
    [Fact]
    public void Rop_Wraps_Value_In_Success_Result()
    {
        var r = 42.Rop();
        Assert.True(r.IsSuccess);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void Rop_Wraps_Null_Reference_In_Success_Result()
    {
        string? value = null;
        var r = value.Rop();
        Assert.True(r.IsSuccess);
        Assert.Null(r.Value);
    }

    [Fact]
    public void Rop_Starts_A_Fluent_Pipeline()
    {
        var r = "john@x.com".Rop()
            .Ensure(e => e.Contains('@'), AxisError.ValidationRule("INVALID_EMAIL"))
            .Map(e => e.ToUpperInvariant());

        Assert.True(r.IsSuccess);
        Assert.Equal("JOHN@X.COM", r.Value);
    }

    [Fact]
    public void Rop_Pipeline_Short_Circuits_On_Failure()
    {
        var r = "invalid".Rop()
            .Ensure(e => e.Contains('@'), AxisError.ValidationRule("INVALID_EMAIL"))
            .Map(e => e.ToUpperInvariant());

        Assert.True(r.IsFailure);
        Assert.Equal("INVALID_EMAIL", r.Errors[0].Code);
    }
}
