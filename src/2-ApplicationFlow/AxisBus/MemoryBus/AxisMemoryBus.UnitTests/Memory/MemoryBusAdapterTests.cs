using AxisMediator.Contracts.CQRS.Events;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace AxisMemoryBus.UnitTests.Memory;

public class MemoryBusAdapterTests
{
    private record TestEvent(string Message) : IAxisEvent;

    // ReSharper disable once NotAccessedPositionalProperty.Local
    private record AnotherEvent(int Value) : IAxisEvent;

    private class SuccessHandler : IAxisEventHandler<TestEvent>
    {
        public bool WasCalled { get; private set; }

        public Task<AxisResult> HandleAsync(TestEvent @event)
        {
            WasCalled = true;
            return Task.FromResult(AxisResult.Ok());
        }
    }

    private class FailureHandler : IAxisEventHandler<TestEvent>
    {
        public Task<AxisResult> HandleAsync(TestEvent @event)
            => Task.FromResult<AxisResult>(AxisError.BusinessRule("HANDLER_FAILED"));
    }

    private class SlowHandler : IAxisEventHandler<TestEvent>
    {
        public bool WasCalled { get; private set; }

        public async Task<AxisResult> HandleAsync(TestEvent @event)
        {
            await Task.Delay(50);
            WasCalled = true;
            return AxisResult.Ok();
        }
    }

    private class ThrowingHandler : IAxisEventHandler<TestEvent>
    {
        public Task<AxisResult> HandleAsync(TestEvent @event)
            => throw new InvalidOperationException("Handler exploded");
    }

    private static MemoryBusAdapter CreateAdapter(IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        return new MemoryBusAdapter(serviceProvider);
    }

    [Fact]
    public async Task PublishAsync_ShouldReturnSuccess_WhenNoHandlersRegistered()
    {
        // Arrange
        var adapter = CreateAdapter(new ServiceCollection());

        // Act
        var result = await adapter.PublishAsync(new TestEvent("hello"));

        // Assert
        result.ShouldSucceed();
    }

    [Fact]
    public async Task PublishAsync_ShouldInvokeHandler_WhenSingleHandlerRegistered()
    {
        // Arrange
        var handler = new SuccessHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(handler);
        var adapter = CreateAdapter(services);

        // Act
        var result = await adapter.PublishAsync(new TestEvent("hello"));

        // Assert
        result.ShouldSucceed();
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task PublishAsync_ShouldInvokeAllHandlers_WhenMultipleHandlersRegistered()
    {
        // Arrange
        var handler1 = new SuccessHandler();
        var handler2 = new SuccessHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(handler1);
        services.AddSingleton<IAxisEventHandler<TestEvent>>(handler2);
        var adapter = CreateAdapter(services);

        // Act
        var result = await adapter.PublishAsync(new TestEvent("hello"));

        // Assert
        result.ShouldSucceed();
        Assert.True(handler1.WasCalled);
        Assert.True(handler2.WasCalled);
    }

    [Fact]
    public async Task PublishAsync_ShouldExecuteHandlersInParallel()
    {
        // Arrange
        var slow1 = new SlowHandler();
        var slow2 = new SlowHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(slow1);
        services.AddSingleton<IAxisEventHandler<TestEvent>>(slow2);
        var adapter = CreateAdapter(services);

        // Act
        var sw = Stopwatch.StartNew();
        var result = await adapter.PublishAsync(new TestEvent("parallel"));
        sw.Stop();

        // Assert
        result.ShouldSucceed();
        Assert.True(slow1.WasCalled);
        Assert.True(slow2.WasCalled);
        // Parallel execution must be measurably faster than sequential (2 * 100ms). Allow generous headroom for CI load.
        Assert.True(sw.ElapsedMilliseconds < 300, $"Expected parallel execution but took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PublishAsync_ShouldReturnFailure_WhenHandlerReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(new FailureHandler());
        var adapter = CreateAdapter(services);

        // Act
        var result = await adapter.PublishAsync(new TestEvent("fail"));

        // Assert
        result.ShouldFail();
    }

    [Fact]
    public async Task PublishAsync_ShouldAggregateErrors_WhenMultipleHandlersFail()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(new FailureHandler());
        services.AddSingleton<IAxisEventHandler<TestEvent>>(new FailureHandler());
        var adapter = CreateAdapter(services);

        // Act
        var result = await adapter.PublishAsync(new TestEvent("fail-both"));

        // Assert
        var errors = result.ShouldFail();
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public async Task PublishAsync_ShouldAggregatePartialFailures_WhenSomeHandlersFail()
    {
        // Arrange
        var successHandler = new SuccessHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(successHandler);
        services.AddSingleton<IAxisEventHandler<TestEvent>>(new FailureHandler());
        var adapter = CreateAdapter(services);

        // Act
        var result = await adapter.PublishAsync(new TestEvent("partial"));

        // Assert
        var errors = result.ShouldFail();
        Assert.Single(errors);
        Assert.True(successHandler.WasCalled);
    }

    [Fact]
    public async Task PublishAsync_ShouldReturnFailure_WhenHandlerThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(new ThrowingHandler());
        var adapter = CreateAdapter(services);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PublishAsync(new TestEvent("throw")));
    }

    [Fact]
    public async Task PublishAsync_ShouldNotInvokeHandlersOfDifferentEventType()
    {
        // Arrange
        var testHandler = new SuccessHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(testHandler);
        var adapter = CreateAdapter(services);

        // Act
        var result = await adapter.PublishAsync(new AnotherEvent(42));

        // Assert
        result.ShouldSucceed();
        Assert.False(testHandler.WasCalled);
    }

    [Fact]
    public async Task PublishAsync_ShouldAcceptTopics_WhenProvided()
    {
        // Arrange
        var handler = new SuccessHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(handler);
        var adapter = CreateAdapter(services);

        // Act
        var result = await adapter.PublishAsync(new TestEvent("with-topics"), "topic-a", "topic-b");

        // Assert
        result.ShouldSucceed();
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task PublishAsync_ShouldWorkWithoutTopics()
    {
        // Arrange
        var handler = new SuccessHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(handler);
        var adapter = CreateAdapter(services);

        // Act
        var result = await adapter.PublishAsync(new TestEvent("no-topics"));

        // Assert
        result.ShouldSucceed();
        Assert.True(handler.WasCalled);
    }

    private class CapturingHandler : IAxisEventHandler<TestEvent>
    {
        public TestEvent? ReceivedEvent { get; private set; }

        public Task<AxisResult> HandleAsync(TestEvent @event)
        {
            ReceivedEvent = @event;
            return Task.FromResult(AxisResult.Ok());
        }
    }

    [Fact]
    public async Task PublishAsync_ShouldPassEventDataToHandler()
    {
        // Arrange
        var handler = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IAxisEventHandler<TestEvent>>(handler);
        var adapter = CreateAdapter(services);
        var expected = new TestEvent("event-data");

        // Act
        await adapter.PublishAsync(expected);

        // Assert
        Assert.NotNull(handler.ReceivedEvent);
        Assert.Equal("event-data", handler.ReceivedEvent.Message);
    }
}
