using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class ActorStampBehaviorTests : CatalogTestHost
{
    private readonly ActorProbe _probe = new();

    [Fact]
    public async Task StampsTheAmbientActorForTheInnerBehaviorsWhenTheFacadeDispatchesAsync()
    {
        var facade = Build<IProductsFacade>(ObserveTheStamp);

        var result = await facade.GetProductAsync(new GetProductQuery { ProductId = ProductId.New });

        result.ShouldSucceed();
        Assert.Equal([new ActorProbe.Observation(Stamped: true, Identity)], _probe.Observations);
        ProductsReader.Verify(port => port.GetByIdAsync(It.IsAny<ProductId>()), Times.Once);
    }

    [Fact]
    public async Task StampsNoActorWhenTheCallerIsAnonymousAsync()
    {
        Identity = null;
        var facade = Build<IProductsFacade>(ObserveTheStamp);

        var result = await facade.GetProductAsync(new GetProductQuery { ProductId = ProductId.New });

        result.ShouldSucceed();
        Assert.Equal([new ActorProbe.Observation(Stamped: true, Actor: null)], _probe.Observations);
    }

    private void ObserveTheStamp(IServiceCollection services)
        => services
            .AddSingleton(_probe)
            .AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(ActorProbeBehavior<,>));
}
