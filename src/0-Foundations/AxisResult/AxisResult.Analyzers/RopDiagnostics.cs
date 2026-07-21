using Microsoft.CodeAnalysis;

namespace Axis.Analyzers;

internal static class RopDiagnostics
{
    public const string Category = "Axis.Result";

    private const string HelpBase = "https://github.com/axis-sys/axis-framework-dotnet/rules/framework/0-foundations/axis-result/";

    // Rule: result-value-access-safety (severity: must)
    public static readonly DiagnosticDescriptor ValueAccess = new(
        id: "AXIS0001",
        title: "Bare .Value read on AxisResult<T>",
        messageFormat: "Do not read '.Value' on an AxisResult<T>; leave the rail with Match or the positional Deconstruct",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AxisResult<T>.Value throws NoAccessValueOnErrorResultException on failure. Extract the value through Match, which forces both branches to be handled, or the positional Deconstruct, which never throws.",
        helpLinkUri: HelpBase + "result-value-access-safety.yaml");

    // Rule: result-no-if-else-flow (severity: must)
    public static readonly DiagnosticDescriptor Branching = new(
        id: "AXIS0002",
        title: "Control flow branches on an AxisResult outcome",
        messageFormat: "Do not branch on '{0}'; compose the outcome with operators (Then/Map/Ensure) or collapse it with Match",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Then, Map and Ensure read the outcome internally and short-circuit on the first failure. Branching on IsSuccess/IsFailure reintroduces the boilerplate the railway model exists to delete; the only sanctioned branch is Match at the terminal.",
        helpLinkUri: HelpBase + "result-no-if-else-flow.yaml");

    // Rule: result-no-throw (severity: must)
    public static readonly DiagnosticDescriptor RailTryCatch = new(
        id: "AXIS0003",
        title: "try/catch on a method that returns AxisResult",
        messageFormat: "Do not use try/catch on the result rail; wrap the throwing boundary with AxisResult.Try/TryAsync and stay on the failure rail",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A fallible operation encodes expected failure as an AxisError on the failure rail, not through catch-based control flow. Convert exceptions at the boundary with Try/TryAsync, which turn non-critical exceptions into an AxisError and rethrow critical ones.",
        helpLinkUri: HelpBase + "result-no-throw.yaml");

    // Rule: result-try-boundary (severity: must)
    public static readonly DiagnosticDescriptor TryBoundary = new(
        id: "AXIS0004",
        title: "AxisResult.Try called without a typed error handler",
        messageFormat: "Pass an explicit errorHandler to '{0}'; the default maps the exception message to the error code, breaking the stable-code contract",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Try/TryAsync/TryBind/TryBindAsync default to AxisError.InternalServerError(ex.Message), placing the raw exception message in the error code. Always pass a typed Func<Exception, AxisError> so the boundary produces a stable, categorized code.",
        helpLinkUri: HelpBase + "result-try-boundary.yaml");

    // Rule: result-error-typing (severity: must)
    public static readonly DiagnosticDescriptor ErrorCodeFromMessage = new(
        id: "AXIS0005",
        title: "AxisError code built from an exception message",
        messageFormat: "Do not build an AxisError code from an exception message; a code is a stable UPPER_SNAKE_CASE constant, not parsed prose",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An AxisError carries a stable Code plus a Type; the Code is a categorized constant used by logs, metrics and retry. Passing an exception's Message as the code leaks unstable prose into the contract — map the exception to a fixed code instead.",
        helpLinkUri: HelpBase + "result-error-typing.yaml");

    // Rule: result-deconstruct-terminal-only (severity: must)
    public static readonly DiagnosticDescriptor DeconstructOnRail = new(
        id: "AXIS0006",
        title: "AxisResult deconstructed on the rail",
        messageFormat: "Do not deconstruct an AxisResult inside a method that itself returns AxisResult; {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The positional Deconstruct (and the positional pattern) is the sanctioned exit at the TERMINAL edge — a controller, a test, a worker loop that collapses the outcome. Inside the rail (a method that returns AxisResult or Task/ValueTask of it) deconstructing re-implements the manual short-circuit that Then/Map/Ensure already provide, laundering `if (result.IsFailure) return` through a tuple.",
        helpLinkUri: HelpBase + "result-deconstruct-terminal-only.yaml");

    // Rule: result-value-access-safety (severity: must)
    public static readonly DiagnosticDescriptor ForgivenDeconstructedValue = new(
        id: "AXIS0007",
        title: "Null-forgiving read of a deconstructed AxisResult value",
        messageFormat: "Do not apply '!' to '{0}' without proving success first; on failure the deconstructed value is default and this becomes a latent NullReferenceException",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The positional Deconstruct never throws: on failure it yields default(T). Consuming that value with the null-forgiving operator without a prior check of the success flag is AxisResult<T>.Value in disguise — the throw is merely deferred to a NullReferenceException at the first dereference. Guard on the success component before forgiving, or collapse with Match.",
        helpLinkUri: HelpBase + "result-value-access-safety.yaml");
}
