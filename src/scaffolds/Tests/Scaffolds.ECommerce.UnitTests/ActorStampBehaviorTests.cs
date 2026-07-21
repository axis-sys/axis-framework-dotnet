namespace Scaffolds.ECommerce.UnitTests;

public sealed class ActorStampBehaviorTests
{
    [Fact]
    public async Task StampsTheAmbientActorAndCallsNextOnceAsync()
    {
        var caller = AxisEntityId.New;
        var mediator = new Mock<IAxisMediator>();
        mediator.SetupGet(m => m.AxisEntityId).Returns(caller);
        var behavior = new ActorStampBehavior<GetProductQuery, GetProductResponse>(mediator.Object);
        var context = new AxisPipelineContext();
        var nextCalls = 0;

        var result = await behavior.HandleAsync(
            new GetProductQuery { ProductId = Guid.NewGuid().ToString() },
            context,
            () =>
            {
                nextCalls++;
                return Task.FromResult<AxisResult<GetProductResponse>>(new GetProductResponse { ProductId = Guid.NewGuid().ToString(), Name = "Keyboard", Stock = 5 });
            });

        result.ShouldSucceed();
        Assert.Equal(1, nextCalls);
        Assert.Equal(caller, context.Get<AxisEntityId?>(PipelineKeys.Actor)!.Value);
    }
}
