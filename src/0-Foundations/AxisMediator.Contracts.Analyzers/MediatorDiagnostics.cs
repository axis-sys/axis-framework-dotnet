using Microsoft.CodeAnalysis;

namespace Axis.Analyzers;

internal static class MediatorDiagnostics
{
    public const string Category = "Axis.Mediator";

    private const string HelpBase = "https://github.com/axis-sys/axis-framework-dotnet/rules/framework/0-foundations/axis-mediator-contracts/";

    // Rule: mediator-cancellation-is-ambient (severity: must)
    public static readonly DiagnosticDescriptor AmbientCancellation = new(
        id: "AXIS0400",
        title: "Handler declares a CancellationToken parameter",
        messageFormat: "Do not declare a CancellationToken parameter on '{0}'; the cancellation token is ambient — read it from IAxisMediator.CancellationToken",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The request cancellation token travels with the ambient execution context, not through method signatures. A type that implements an Axis handler interface reads the token from IAxisMediator.CancellationToken; adding a CancellationToken parameter reintroduces manual threading and risks using the wrong token when a scope is nested or replaced.",
        helpLinkUri: HelpBase + "mediator-cancellation-is-ambient.yaml");

    // Rule: mediator-dispatch-surface (severity: must)
    public static readonly DiagnosticDescriptor DispatchInHandler = new(
        id: "AXIS0401",
        title: "CQRS dispatch issued from inside a handler",
        messageFormat: "Do not dispatch through mediator.Cqrs inside a handler; compose cross-use-case dispatch in the Facade",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Dispatch through IAxisMediator.Cqrs is issued from the Facade that exposes a use case, never from inside a handler. A handler that dispatches another command or query creates hidden coupling between use cases; orchestration across use cases belongs in the Facade.",
        helpLinkUri: HelpBase + "mediator-dispatch-surface.yaml");

    // Rule: mediator-ambient-context-access (severity: must)
    public static readonly DiagnosticDescriptor ContextAccessorInHandler = new(
        id: "AXIS0402",
        title: "Handler injects IAxisMediatorContextAccessor",
        messageFormat: "Do not inject IAxisMediatorContextAccessor into a handler; read the ambient context through IAxisMediator",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "IAxisMediatorContextAccessor is the writable seam that application middleware uses to seed the ambient context at the edge. A handler consumes that context through the single read-only IAxisMediator; injecting the writable accessor reaches behind the abstraction and lets a handler mutate ambient state.",
        helpLinkUri: HelpBase + "mediator-ambient-context-access.yaml");

    // Rule: mediator-pipeline-context (severity: should)
    public static readonly DiagnosticDescriptor PipelineContextLiteralKey = new(
        id: "AXIS0403",
        title: "String-literal key on AxisPipelineContext",
        messageFormat: "Do not pass a string-literal key to AxisPipelineContext.{0}; use an AxisPipelineContextKeys constant",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "AxisPipelineContext keys are ordinal case-sensitive and shared across behaviors, so an inline string literal is a silent typo waiting to happen. Route every Get/Set key through a named AxisPipelineContextKeys constant so producers and consumers agree on one spelling.",
        helpLinkUri: HelpBase + "mediator-pipeline-context.yaml");
}
