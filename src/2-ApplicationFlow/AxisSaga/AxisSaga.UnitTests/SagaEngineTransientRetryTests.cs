using Axis.Contracts.Configuration;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;

namespace AxisSaga.UnitTests;

public class SagaEngineTransientRetryTests
{
    public record TestPayload
    {
        public string SagaId { get; init; } = "";
    }

    private const string SagaName = "RetrySaga";
    private const string StageName = "OnlyStage";

    private static AxisSagaDefinition Definition(int maxAttempts) =>
        AxisSagaDefinitions.Define<TestPayload>(SagaName, saga =>
            saga.AddStage(StageName)
                .RetryOnTransient(maxAttempts, TimeSpan.FromMilliseconds(1))
                .FinishOnSuccess());

    private static AxisSagaInstance PendingInstance() => new()
    {
        SagaId = "saga-1",
        SagaName = SagaName,
        Status = AxisSagaStatus.Pending,
        CurrentStage = null,
        PayloadJson = "{}",
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static AxisResult<object?> Transient() => AxisError.ServiceUnavailable("DB_TRANSIENT");
    private static AxisResult<object?> Success() => AxisResult.Ok<object?>(new TestPayload());

    private static (SagaEngine Engine, Mock<ISagaInstanceStore> Instances, Mock<ISagaStageHandlerInvoker> Handlers)
        BuildEngine(AxisSagaDefinition def)
    {
        var registry = new Mock<IAxisSagaDefinitionRegistry>();
        registry.Setup(r => r.Get(SagaName)).Returns(def);

        var instances = new Mock<ISagaInstanceStore>();
        instances.Setup(i => i.AcquireLeaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(PendingInstance());
        instances.Setup(i => i.ExtendLeaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        instances.Setup(i => i.MoveToStatusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<AxisSagaStatus>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(AxisResult.Ok());
        instances.Setup(i => i.PersistStageSuccessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(AxisResult.Ok());
        instances.Setup(i => i.CompleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(AxisResult.Ok());
        instances.Setup(i => i.FailAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(AxisResult.Ok());

        var logs = new Mock<ISagaStageLogStore>();
        logs.Setup(l => l.IsCompletedAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        logs.Setup(l => l.WriteStartedAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("log-1");

        var handlers = new Mock<ISagaStageHandlerInvoker>();
        var logger = new Mock<IAxisLogger<SagaEngine>>();

        var settings = new AxisSagaSettings
        {
            ConnectionString = "unused",
            ResumeAfter = TimeSpan.FromMinutes(5),
            ResumerEnabled = false,
        };

        var engine = new SagaEngine(registry.Object, instances.Object, logs.Object,
            handlers.Object, settings, logger.Object);

        return (engine, instances, handlers);
    }

    [Fact]
    public async Task ForwardStage_RetriesTransientFailure_ThenCompletesAsync()
    {
        var (engine, instances, handlers) = BuildEngine(Definition(maxAttempts: 3));
        handlers.SetupSequence(h => h.InvokeAsync(It.IsAny<Type>(), SagaName, StageName, It.IsAny<object>()))
            .ReturnsAsync(Transient())
            .ReturnsAsync(Transient())
            .ReturnsAsync(Success());

        var result = await engine.ExecuteAsync("saga-1");

        Assert.True(result.IsSuccess);
        handlers.Verify(h => h.InvokeAsync(It.IsAny<Type>(), SagaName, StageName, It.IsAny<object>()), Times.Exactly(3));
        instances.Verify(i => i.CompleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        instances.Verify(i => i.FailAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ForwardStage_ExhaustsTransientRetries_ThenFailsAsync()
    {
        var (engine, instances, handlers) = BuildEngine(Definition(maxAttempts: 3));
        handlers.Setup(h => h.InvokeAsync(It.IsAny<Type>(), SagaName, StageName, It.IsAny<object>()))
            .ReturnsAsync(Transient());

        await engine.ExecuteAsync("saga-1");

        handlers.Verify(h => h.InvokeAsync(It.IsAny<Type>(), SagaName, StageName, It.IsAny<object>()), Times.Exactly(3));
        instances.Verify(i => i.FailAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        instances.Verify(i => i.CompleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForwardStage_NonTransientFailure_DoesNotRetryAsync()
    {
        var (engine, instances, handlers) = BuildEngine(Definition(maxAttempts: 3));
        handlers.Setup(h => h.InvokeAsync(It.IsAny<Type>(), SagaName, StageName, It.IsAny<object>()))
            .ReturnsAsync(AxisResult.Error<object?>(AxisError.BusinessRule("NOT_TRANSIENT")));

        await engine.ExecuteAsync("saga-1");

        handlers.Verify(h => h.InvokeAsync(It.IsAny<Type>(), SagaName, StageName, It.IsAny<object>()), Times.Once);
        instances.Verify(i => i.FailAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ForwardStage_SingleAttemptCap_DisablesRetryAsync()
    {
        var (engine, _, handlers) = BuildEngine(Definition(maxAttempts: 1));
        handlers.Setup(h => h.InvokeAsync(It.IsAny<Type>(), SagaName, StageName, It.IsAny<object>()))
            .ReturnsAsync(Transient());

        await engine.ExecuteAsync("saga-1");

        handlers.Verify(h => h.InvokeAsync(It.IsAny<Type>(), SagaName, StageName, It.IsAny<object>()), Times.Once);
    }
}
