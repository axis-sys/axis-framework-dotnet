using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AxisMemoryCache.UnitTests.Memory;

internal class AxisMediatorAccessor : IAxisMediatorAccessor
{
    private static readonly AsyncLocal<IAxisMediator?> _axisMediator = new();

    public IAxisMediator? AxisMediator
    {
        get => _axisMediator.Value;
        set => _axisMediator.Value = value;
    }
}

public class DependencyInjectionTests
{
    [Fact]
    public void AddAxisMemoryCache_ShouldRegisterIAxisCacheAsMemoryCacheAdapter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAxisMemoryCache();
        services.AddSingleton<IAxisMediatorAccessor>(_ =>
        {
            var mediator = new Mock<IAxisMediator>();
            mediator.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
            return new AxisMediatorAccessor
            {
                AxisMediator = mediator.Object
            };
        });
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var cache = serviceProvider.GetService<IAxisCache>();
        Assert.NotNull(cache);
        Assert.IsType<MemoryCacheAdapter>(cache);
    }
}
