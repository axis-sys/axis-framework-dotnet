using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace AxisMediator.UnitTests.CQRS;

public class AxisMediatorContextAndAccessorTests : BaseUnitTest
{
    [Fact]
    public void AxisMediatorExposesContextAccessorValues()
    {
        var serviceProvider = DefaultServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IAxisMediatorContextAccessor>();
        var identity = AxisEntityId.New;
        ctx.AxisEntityId = identity;
        ctx.JourneyId = "journey-42";
        ctx.CancellationToken = CancellationToken.None;

        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        Assert.Equal(identity, mediator.AxisEntityId);
        Assert.Equal("journey-42", mediator.JourneyId);
        Assert.False(string.IsNullOrEmpty(mediator.TraceId));
    }

    [Fact]
    public void TraceIdUsesActivityWhenPresent()
    {
        using var activity = new Activity("test-activity");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        var serviceProvider = DefaultServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        Assert.Equal(activity.TraceId.ToString(), mediator.TraceId);
    }

    [Fact]
    public void DisposeClearsAccessorMediator()
    {
        var serviceProvider = DefaultServiceProvider();
        var accessor = serviceProvider.GetRequiredService<IAxisMediatorAccessor>();

        var scope = serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();
        Assert.Same(mediator, accessor.AxisMediator);

        scope.Dispose();
        Assert.Null(accessor.AxisMediator);
    }

    [Fact]
    public void IsAuthenticatedDefaultPropertyReturnsTrueWhenIdentityPresent()
    {
        var serviceProvider = DefaultServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IAxisMediatorContextAccessor>();
        ctx.AxisEntityId = AxisEntityId.New;

        Assert.True(ctx.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticatedDefaultPropertyReturnsFalseWhenIdentityMissing()
    {
        var serviceProvider = DefaultServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IAxisMediatorContextAccessor>();
        ctx.AxisEntityId = null;

        Assert.False(ctx.IsAuthenticated);
    }

    [Fact]
    public void OriginIdGetterReturnsContextValue()
    {
        var serviceProvider = DefaultServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IAxisMediatorContextAccessor>();
        ctx.OriginId = "origin-xyz";

        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();
        Assert.Equal("origin-xyz", mediator.OriginId);
    }

    [Fact]
    public void CancellationTokenGetterReturnsContextValue()
    {
        var serviceProvider = DefaultServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IAxisMediatorContextAccessor>();
        using var cts = new CancellationTokenSource();
        ctx.CancellationToken = cts.Token;

        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();
        Assert.Equal(cts.Token, mediator.CancellationToken);
    }
}
