using Axis.Contracts.Configuration;

namespace Axis.Core;

internal class AxisSagaStageBuilder<TPayload>(string stageName, bool isErrorStage)
    : IAxisSagaStageBuilder<TPayload> where TPayload : class
{
    private readonly List<string> _onErrorRoute = [];
    private string? _onSuccessNext;
    private bool _onSuccessFinish;
    private int? _transientRetryMaxAttempts;
    private TimeSpan? _transientRetryBaseDelay;

    internal string StageName => stageName;
    internal bool IsErrorStage => isErrorStage;

    public IAxisSagaStageBuilder<TPayload> NextStageOnSuccess(string nextStageName)
    {
        if (string.IsNullOrWhiteSpace(nextStageName))
            throw new ArgumentException("Next stage name cannot be null or empty.", nameof(nextStageName));
        if (_onSuccessFinish)
            throw new InvalidOperationException($"Stage '{stageName}' already configured as Finish; cannot also set Next.");

        _onSuccessNext = nextStageName;
        return this;
    }

    public IAxisSagaStageBuilder<TPayload> FinishOnSuccess()
    {
        if (_onSuccessNext is not null)
            throw new InvalidOperationException($"Stage '{stageName}' already has Next; cannot also be Finish.");

        _onSuccessFinish = true;
        return this;
    }

    public IAxisSagaStageBuilder<TPayload> RouteToOnError(params string[] errorStageNames)
    {
        if (errorStageNames is null)
            throw new ArgumentNullException(nameof(errorStageNames));

        foreach (var name in errorStageNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Error stage name cannot be null or empty.", nameof(errorStageNames));
            _onErrorRoute.Add(name);
        }
        return this;
    }

    public IAxisSagaStageBuilder<TPayload> RetryOnTransient(int maxAttempts, TimeSpan? baseDelay = null)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Max attempts must be at least 1.");
        if (baseDelay is { } delay && delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), baseDelay, "Base delay cannot be negative.");

        _transientRetryMaxAttempts = maxAttempts;
        _transientRetryBaseDelay = baseDelay;
        return this;
    }

    internal AxisSagaStageDefinition Compile() => new()
    {
        StageName = stageName,
        IsErrorStage = isErrorStage,
        NextStageOnSuccess = _onSuccessNext,
        RouteToOnError = _onErrorRoute.AsReadOnly(),
        TransientRetryMaxAttempts = _transientRetryMaxAttempts,
        TransientRetryBaseDelay = _transientRetryBaseDelay
    };
}
