using Microsoft.CodeAnalysis;

namespace Axis.Analyzers;

internal static class BusDiagnostics
{
    public const string Category = "Axis.Bus";

    private const string HelpBase = "https://github.com/axis-sys/axis-framework-dotnet/rules/framework/2-application-flow/axis-bus/";

    // Rule: bus-outbox-enqueue-in-uow-transaction (severity: must)
    public static readonly DiagnosticDescriptor PublishAfterSave = new(
        id: "AXIS0300",
        title: "Event published after the unit of work commit",
        messageFormat: "Publish the event BEFORE SaveChangesAsync; the outbox enqueues on the unit of work and the commit drains it — published after the commit, the event is stranded outside the transaction",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "With the atomic outbox, IAxisBus.PublishAsync enqueues the event on the request-scoped unit of work and SaveChangesAsync drains that queue into its own transaction at commit, so the event and the business state change commit together. Publishing after the commit leaves the event on a queue no commit will drain — it is silently lost, defeating the outbox.",
        helpLinkUri: HelpBase + "bus-outbox-enqueue-in-uow-transaction.yaml");
}
