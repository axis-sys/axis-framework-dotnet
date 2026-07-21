namespace Axis.Ports;

/// <summary>
/// Persistence boundary for the stage-attempt log (<c>SAGA_STAGE_LOGS</c>). The engine writes one row
/// per stage attempt (Started → Completed | Failed) so the resumer can skip stages that already
/// succeeded after a crash. Write failures are logged and swallowed by the implementation: a missing
/// log line is recoverable (the next run just re-executes the stage), so it is never propagated.
/// </summary>
public interface ISagaStageLogStore
{
    Task<bool> IsCompletedAsync(string sagaId, string stageName);

    Task<string> WriteStartedAsync(string sagaId, string stageName);

    Task MarkCompletedAsync(string logId);

    Task MarkFailedAsync(string logId, string errorCode, string? errorMessage);
}
