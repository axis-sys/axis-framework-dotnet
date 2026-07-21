namespace AxisMediator.Contracts.Pipelines;

/// <summary>Well-known keys written into <see cref="AxisPipelineContext"/> by built-in behaviors.</summary>
public static class AxisPipelineContextKeys
{
    /// <summary>The active <c>IAxisSpan</c> for the current request, set by TelemetryBehavior.</summary>
    public const string Span = "axis.pipeline.span";
}
