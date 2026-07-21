namespace AxisUnitTests;

public class IsTransientFailureTests
{
    private static readonly AxisError Transient1 = AxisError.ServiceUnavailable("DEP_DOWN");
    private static readonly AxisError Transient2 = AxisError.Timeout("DEP_SLOW");
    private static readonly AxisError Terminal = AxisError.ValidationRule("BAD_INPUT");

    [Fact]
    public void Success_Is_Not_A_Transient_Failure()
    {
        var r = AxisResult.Ok();
        Assert.False(r.IsTransientFailure);
    }

    [Fact]
    public void Generic_Success_Is_Not_A_Transient_Failure()
    {
        var r = AxisResult.Ok(42);
        Assert.False(r.IsTransientFailure);
    }

    [Fact]
    public void Single_Transient_Error_Is_A_Transient_Failure()
    {
        var r = AxisResult.Error(Transient1);
        Assert.True(r.IsTransientFailure);
    }

    [Fact]
    public void All_Transient_Errors_Is_A_Transient_Failure()
    {
        var r = AxisResult.Error([Transient1, Transient2]);
        Assert.True(r.IsTransientFailure);
    }

    [Fact]
    public void Single_Terminal_Error_Is_Not_A_Transient_Failure()
    {
        var r = AxisResult.Error(Terminal);
        Assert.False(r.IsTransientFailure);
    }

    [Fact]
    public void Mixed_Transient_And_Terminal_Errors_Is_Not_A_Transient_Failure()
    {
        var r = AxisResult.Error([Transient1, Terminal]);
        Assert.False(r.IsTransientFailure);
    }

    [Fact]
    public void Generic_Failure_Follows_The_Same_Classification()
    {
        var transient = AxisResult.Error<int>(Transient1);
        var terminal = AxisResult.Error<int>(Terminal);

        Assert.True(transient.IsTransientFailure);
        Assert.False(terminal.IsTransientFailure);
    }
}
