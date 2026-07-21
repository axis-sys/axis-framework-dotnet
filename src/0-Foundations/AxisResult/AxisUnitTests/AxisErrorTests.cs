namespace AxisUnitTests;

public class AxisErrorTests
{
    [Fact]
    public void Factory_InternalServerError_Creates_Correctly()
    {
        var e = AxisError.InternalServerError("ISE");
        Assert.Equal("ISE", e.Code);
        Assert.Equal(AxisErrorType.InternalServerError, e.Type);
    }

    [Fact]
    public void Factory_ValidationRule_Creates_Correctly()
    {
        var e = AxisError.ValidationRule("V1");
        Assert.Equal(AxisErrorType.ValidationRule, e.Type);
    }

    [Fact]
    public void Factory_NotFound_Creates_Correctly()
    {
        var e = AxisError.NotFound("NF");
        Assert.Equal(AxisErrorType.NotFound, e.Type);
    }

    [Fact]
    public void Factory_Conflict_Creates_Correctly()
    {
        var e = AxisError.Conflict("C1");
        Assert.Equal(AxisErrorType.Conflict, e.Type);
    }

    [Fact]
    public void Factory_BusinessRule_Creates_Correctly()
    {
        var e = AxisError.BusinessRule("B1");
        Assert.Equal(AxisErrorType.BusinessRule, e.Type);
    }

    [Fact]
    public void Factory_Unauthorized_Creates_Correctly()
    {
        var e = AxisError.Unauthorized("U1");
        Assert.Equal(AxisErrorType.Unauthorized, e.Type);
    }

    [Fact]
    public void Factory_Forbidden_Creates_Correctly()
    {
        var e = AxisError.Forbidden("F1");
        Assert.Equal(AxisErrorType.Forbidden, e.Type);
    }

    [Fact]
    public void Factory_ServiceUnavailable_Creates_Correctly()
    {
        var e = AxisError.ServiceUnavailable("SU");
        Assert.Equal(AxisErrorType.ServiceUnavailable, e.Type);
    }

    [Fact]
    public void Factory_Timeout_Creates_Correctly()
    {
        var e = AxisError.Timeout("T1");
        Assert.Equal(AxisErrorType.Timeout, e.Type);
    }

    [Fact]
    public void Factory_TooManyRequests_Creates_Correctly()
    {
        var e = AxisError.TooManyRequests("TMR");
        Assert.Equal(AxisErrorType.TooManyRequests, e.Type);
    }

    [Fact]
    public void Factory_GatewayTimeout_Creates_Correctly()
    {
        var e = AxisError.GatewayTimeout("GT");
        Assert.Equal(AxisErrorType.GatewayTimeout, e.Type);
    }

    [Fact]
    public void Factory_Mapping_Creates_Correctly()
    {
        var e = AxisError.Mapping("M1");
        Assert.Equal(AxisErrorType.Mapping, e.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Factory_Falls_Back_To_Sentinel_On_Blank_Code(string? code)
    {
        var e = AxisError.NotFound(code!);
        Assert.Equal(AxisError.MissingCodeSentinel, e.Code);
        Assert.Equal(AxisErrorType.NotFound, e.Type);
    }

    [Fact]
    public void Factory_Does_Not_Throw_On_Blank_Code()
    {
        var ex = Record.Exception(() => AxisError.InternalServerError(null!));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(AxisErrorType.ServiceUnavailable, true)]
    [InlineData(AxisErrorType.Timeout, true)]
    [InlineData(AxisErrorType.TooManyRequests, true)]
    [InlineData(AxisErrorType.GatewayTimeout, true)]
    [InlineData(AxisErrorType.NotFound, false)]
    [InlineData(AxisErrorType.ValidationRule, false)]
    [InlineData(AxisErrorType.Conflict, false)]
    [InlineData(AxisErrorType.BusinessRule, false)]
    [InlineData(AxisErrorType.Unauthorized, false)]
    [InlineData(AxisErrorType.Forbidden, false)]
    [InlineData(AxisErrorType.Mapping, false)]
    [InlineData(AxisErrorType.InternalServerError, false)]
    public void IsTransient_Is_Correct_For_Each_Type(AxisErrorType type, bool expected)
    {
        var e = type switch
        {
            AxisErrorType.ServiceUnavailable => AxisError.ServiceUnavailable("x"),
            AxisErrorType.Timeout => AxisError.Timeout("x"),
            AxisErrorType.TooManyRequests => AxisError.TooManyRequests("x"),
            AxisErrorType.GatewayTimeout => AxisError.GatewayTimeout("x"),
            AxisErrorType.NotFound => AxisError.NotFound("x"),
            AxisErrorType.ValidationRule => AxisError.ValidationRule("x"),
            AxisErrorType.Conflict => AxisError.Conflict("x"),
            AxisErrorType.BusinessRule => AxisError.BusinessRule("x"),
            AxisErrorType.Unauthorized => AxisError.Unauthorized("x"),
            AxisErrorType.Forbidden => AxisError.Forbidden("x"),
            AxisErrorType.Mapping => AxisError.Mapping("x"),
            _ => AxisError.InternalServerError("x")
        };
        Assert.Equal(expected, e.IsTransient);
    }

    [Fact]
    public void Record_Equality_Works()
    {
        var a = AxisError.NotFound("SAME");
        var b = AxisError.NotFound("SAME");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Record_Equality_Differs_By_Code()
    {
        var a = AxisError.NotFound("A");
        var b = AxisError.NotFound("B");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Record_Equality_Differs_By_Type()
    {
        var a = AxisError.NotFound("SAME");
        var b = AxisError.Conflict("SAME");
        Assert.NotEqual(a, b);
    }
}
