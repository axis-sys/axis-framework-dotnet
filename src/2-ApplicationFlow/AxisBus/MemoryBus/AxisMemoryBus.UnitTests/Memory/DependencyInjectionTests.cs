using Microsoft.Extensions.DependencyInjection;

namespace AxisMemoryBus.UnitTests.Memory;

public class DependencyInjectionTests
{
    [Fact]
    public void AddMemoryBus_ShouldRegisterIAxisBusAsMemoryBusAdapter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAxisMemoryBus();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var bus = serviceProvider.GetService<IAxisBus>();
        Assert.NotNull(bus);
        Assert.IsType<MemoryBusAdapter>(bus);
    }

    [Fact]
    public void AddMemoryBus_ShouldRegisterAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAxisMemoryBus();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var instance1 = serviceProvider.GetService<IAxisBus>();
        var instance2 = serviceProvider.GetService<IAxisBus>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void AddMemoryBus_ShouldReturnServicesForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAxisMemoryBus();

        // Assert
        Assert.Same(services, result);
    }
}
