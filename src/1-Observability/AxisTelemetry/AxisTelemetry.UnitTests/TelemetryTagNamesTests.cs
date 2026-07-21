namespace AxisTelemetry.UnitTests;

// Golden test of the telemetry tag CONTRACT.
//
// The keys below are NOT an implementation detail: they are consumed by dashboard and alert
// queries in the sink (Grafana/Datadog/etc.), which live OUTSIDE this repo and cannot be
// exercised by a test from here. Renaming a key is an observable breaking change — which is
// what happened in 3acbf9a (`axis.axis_identity` → `axis.axis_entity_id`), which went green
// because nothing pinned the literal.
//
// This test turns that silent rename into a red build: editing a tag constant breaks the
// assert and forces migrating the consumers in the same PR.
// See: produto/learnings/routed-L0001-rename-tag-telemetria-quebra-dashboards.md
public class TelemetryTagNamesTests
{
    [Fact]
    public void FrameworkTagKeysMatchTheObservableContract()
    {
        Assert.Equal("axis.axis_entity_id", TelemetryTagNames.AxisEntityId);
        Assert.Equal("axis.trace_id", TelemetryTagNames.TraceId);
        Assert.Equal("axis.journey_id", TelemetryTagNames.JourneyId);
        Assert.Equal("axis.request_type", TelemetryTagNames.RequestType);
        Assert.Equal("axis.request_name", TelemetryTagNames.RequestName);
        Assert.Equal("axis.result_success", TelemetryTagNames.ResultSuccess);
        Assert.Equal("axis.error_codes", TelemetryTagNames.ErrorCodes);
        Assert.Equal("axis.exception_type", TelemetryTagNames.ExceptionType);
    }

    [Fact]
    public void AuthTagKeysMatchTheObservableContract()
    {
        Assert.Equal("auth.scheme", AuthTelemetryTagNames.Scheme);
        Assert.Equal("auth.result", AuthTelemetryTagNames.Result);
        Assert.Equal("auth.failure_reason", AuthTelemetryTagNames.FailureReason);
        Assert.Equal("auth.api_id", AuthTelemetryTagNames.ApiId);
        Assert.Equal("auth.brute_force_suspected", AuthTelemetryTagNames.BruteForceSuspected);
    }
}
