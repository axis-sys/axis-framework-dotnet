using AxisMediator.Contracts.CQRS;
using AxisValidator;

namespace AxisMediator.UnitTests.Pipelines;

public class ValidationBehaviorTests
{
    private record TestCommand : IAxisRequest;
    private record TestResponse : IAxisResponse;

    private class SuccessValidator : IAxisValidator<TestCommand>
    {
        public AxisResult Validate(TestCommand instance) => AxisResult.Ok();
        public Task<AxisResult> ValidateAsync(TestCommand instance) => Task.FromResult(AxisResult.Ok());
    }

    private class FailureValidator : IAxisValidator<TestCommand>
    {
        public AxisResult Validate(TestCommand instance) => AxisError.ValidationRule("INVALID_COMMAND");
        public Task<AxisResult> ValidateAsync(TestCommand instance) => Task.FromResult<AxisResult>(AxisError.ValidationRule("INVALID_COMMAND"));
    }

    private class StubServiceProvider(IAxisValidator<TestCommand>? validator) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IAxisValidator<TestCommand>) ? validator : null;
    }

    private class ThrowingServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
            => throw new InvalidOperationException("Simulated DI resolution failure");
    }

    // ── IPipelineBehavior<TRequest> (non-generic / void) ────────────────────

    [Fact]
    public async Task NonGeneric_WhenValidationPasses_CallsNext()
    {
        var behavior = new ValidationBehavior<TestCommand>(new StubServiceProvider(new SuccessValidator()));
        var nextCalled = false;

        var result = await behavior.HandleAsync(new TestCommand(), new(), () =>
        {
            nextCalled = true;
            return Task.FromResult(AxisResult.Ok());
        });

        Assert.True(nextCalled);
        result.ShouldSucceed();
    }

    [Fact]
    public async Task NonGeneric_WhenValidationFails_DoesNotCallNext()
    {
        var behavior = new ValidationBehavior<TestCommand>(new StubServiceProvider(new FailureValidator()));
        var nextCalled = false;

        var result = await behavior.HandleAsync(new TestCommand(), new(), () =>
        {
            nextCalled = true;
            return Task.FromResult(AxisResult.Ok());
        });

        Assert.False(nextCalled);
        result.ShouldFail();
    }

    [Fact]
    public async Task NonGeneric_WhenNoValidatorRegistered_CallsNext()
    {
        var behavior = new ValidationBehavior<TestCommand>(new StubServiceProvider(null));
        var nextCalled = false;

        var result = await behavior.HandleAsync(new TestCommand(), new(), () =>
        {
            nextCalled = true;
            return Task.FromResult(AxisResult.Ok());
        });

        Assert.True(nextCalled);
        result.ShouldSucceed();
    }

    // ── IPipelineBehavior<TRequest, TResponse> (generic) ────────────────────

    [Fact]
    public async Task Generic_WhenValidationPasses_CallsNext()
    {
        var behavior = new ValidationBehavior<TestCommand, TestResponse>(new StubServiceProvider(new SuccessValidator()));
        var nextCalled = false;

        var result = await behavior.HandleAsync(new TestCommand(), new(), () =>
        {
            nextCalled = true;
            return Task.FromResult(AxisResult.Ok(new TestResponse()));
        });

        Assert.True(nextCalled);
        result.ShouldSucceed();
    }

    [Fact]
    public async Task Generic_WhenValidationFails_DoesNotCallNext()
    {
        var behavior = new ValidationBehavior<TestCommand, TestResponse>(new StubServiceProvider(new FailureValidator()));
        var nextCalled = false;

        var result = await behavior.HandleAsync(new TestCommand(), new(), () =>
        {
            nextCalled = true;
            return Task.FromResult(AxisResult.Ok(new TestResponse()));
        });

        Assert.False(nextCalled);
        result.ShouldFail();
    }

    [Fact]
    public async Task Generic_WhenValidationFails_PropagatesErrors()
    {
        var behavior = new ValidationBehavior<TestCommand, TestResponse>(new StubServiceProvider(new FailureValidator()));

        var result = await behavior.HandleAsync(new TestCommand(), new(), () =>
            Task.FromResult(AxisResult.Ok(new TestResponse())));

        Assert.Single(result.Errors);
        Assert.Equal("INVALID_COMMAND", result.Errors[0].Code);
    }

    [Fact]
    public async Task Generic_WhenNoValidatorRegistered_CallsNext()
    {
        var behavior = new ValidationBehavior<TestCommand, TestResponse>(new StubServiceProvider(null));
        var nextCalled = false;

        var result = await behavior.HandleAsync(new TestCommand(), new(), () =>
        {
            nextCalled = true;
            return Task.FromResult(AxisResult.Ok(new TestResponse()));
        });

        Assert.True(nextCalled);
        result.ShouldSucceed();
    }

    // ── DI resolution failure propagation ────────────────────────────────────

    [Fact]
    public async Task NonGeneric_WhenServiceProviderThrows_PropagatesException()
    {
        var behavior = new ValidationBehavior<TestCommand>(new ThrowingServiceProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(new TestCommand(), new(), () => Task.FromResult(AxisResult.Ok())));
    }

    [Fact]
    public async Task Generic_WhenServiceProviderThrows_PropagatesException()
    {
        var behavior = new ValidationBehavior<TestCommand, TestResponse>(new ThrowingServiceProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(new TestCommand(), new(), () =>
                Task.FromResult(AxisResult.Ok(new TestResponse()))));
    }
}
