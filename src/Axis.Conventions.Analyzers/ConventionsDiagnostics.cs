using Microsoft.CodeAnalysis;

namespace Axis.Conventions.Analyzers;

internal static class ConventionsDiagnostics
{
    // Each concern uses its own Category, so an entire set can be toggled via
    // dotnet_analyzer_diagnostic.category-<Category>.severity in the .editorconfig.
    public const string ArchitectureCategory = "Axis.Architecture";
    public const string EdgeCategory = "Axis.Edge";
    public const string TestingCategory = "Axis.Testing";
    public const string StyleCategory = "Axis.Style";

    // Each diagnostic links to its section of the analyzers page of the application guide.
    private const string HelpBase = "https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#";

    public static readonly DiagnosticDescriptor HandlerInternalSealed = new(
        id: "AXIS0600",
        title: "Use-case handler is not internal sealed",
        messageFormat: "Handler '{0}' must be declared 'internal sealed'; handlers are reached through assembly scanning and the Facade, never as a public type",
        category: ArchitectureCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A use-case handler is an internal sealed class implementing an Axis command or query handler interface; it is registered by assembly scanning and invoked only through the mediator behind a Facade, so it never crosses a project boundary as a public type.",
        helpLinkUri: HelpBase + "axis0600");

    public static readonly DiagnosticDescriptor ValidatorInternalSealed = new(
        id: "AXIS0601",
        title: "Validator is not internal sealed",
        messageFormat: "Validator '{0}' must be declared 'internal sealed'; a validator is an implementation detail discovered by scanning and run by the ValidationBehavior, never a public type",
        category: ArchitectureCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Input validation is an internal sealed {UseCase}Validator deriving AxisValidatorBase<T>; it is discovered by assembly scanning and run by the mediator's ValidationBehavior, so it never crosses a project boundary as a public type.",
        helpLinkUri: HelpBase + "axis0601");

    public static readonly DiagnosticDescriptor ControllerSealed = new(
        id: "AXIS0602",
        title: "Controller is not sealed",
        messageFormat: "Controller '{0}' must be declared 'sealed'; a controller is a leaf edge type that is never inherited",
        category: EdgeCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An HTTP controller inherits ControllerBase and is a sealed leaf — it is never subclassed. Leaving it open (or inheriting the view-aware Controller base) contradicts the edge convention.",
        helpLinkUri: HelpBase + "axis0602");

    public static readonly DiagnosticDescriptor TestMethodNaming = new(
        id: "AXIS0603",
        title: "Test method name is not PascalCase with an Async suffix",
        messageFormat: "Test method '{0}' should be PascalCase '{{WhatItDoes}}When{{Condition}}' with an Async suffix on async tests, not snake_case",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Test methods read as the specification of the case under test: PascalCase {WhatItDoes}When{Condition}, with the Async suffix on asynchronous tests. A snake_case sentence style or a missing Async suffix diverges from the convention.",
        helpLinkUri: HelpBase + "axis0603");

    public static readonly DiagnosticDescriptor DomainPropertiesRecordInternalSealed = new(
        id: "AXIS0604",
        title: "Domain properties record is not internal sealed",
        messageFormat: "Type '{0}' implements a domain properties interface (I…Properties) declared in the same project and must be 'internal sealed'; only the interface is public, the concrete record stays internal",
        category: ArchitectureCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The domain exposes entity state through a public I{Entity}EntityProperties (or I{Entity}Properties) interface and hides the concrete shape behind an internal sealed record implementing it. A public concrete record lets other layers construct domain state directly, bypassing the factory and the port contract that traffics in the interface.",
        helpLinkUri: HelpBase + "axis0604");

    public static readonly DiagnosticDescriptor RouteInjectedCommandJsonIgnore = new(
        id: "AXIS0605",
        title: "Route-injected command property is not [JsonIgnore]",
        messageFormat: "Property '{0}' is populated from a route parameter through a 'with' expression but is not [JsonIgnore], so a value sent in the request body is silently overwritten by the route",
        category: EdgeCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a controller injects a route value into a command via 'command with { Id = routeId }', the target property must be [JsonIgnore] so the request body cannot also supply it. Otherwise a caller can send one id in the body and another in the route, and the body value is dropped without warning.",
        helpLinkUri: HelpBase + "axis0605");

    public static readonly DiagnosticDescriptor EntityPropertiesRecordParameterPerLine = new(
        id: "AXIS0606",
        title: "Entity properties record crams its constructor parameters onto shared lines",
        messageFormat: "Record '{0}' implements an entity properties interface (I…Properties) and declares two or more constructor parameters — put each parameter on its own line so the entity's shape reads vertically",
        category: StyleCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A domain {Entity}Properties record and a repository {Entity}DbEntity carry the persisted shape of an entity in their primary constructor. With two or more parameters, each parameter goes on its own line (none sharing the record-declaration line) — the vertical layout a reader scans an entity/row against; cramming parameters onto one line hides the shape.",
        helpLinkUri: HelpBase + "axis0606");

    public static readonly DiagnosticDescriptor MultiplePublicCompositionExtensions = new(
        id: "AXIS0607",
        title: "More than one public DependencyInjection class in the same assembly",
        messageFormat: "Type '{0}' is a public 'DependencyInjection' class, but this assembly already declares {1} of them; only the project's single composition entry may be public — a per-feature decomposition stays 'internal static class DependencyInjection'",
        category: ArchitectureCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A project exposes exactly one public registration extension as its whole composition surface. A per-bounded-context or per-feature decomposition of that extension — one DependencyInjection class per folder that registers services — stays internal and is called only by the project's single public entry, never crossing the assembly boundary itself.",
        helpLinkUri: HelpBase + "axis0607",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor ErrorsMemberRebuiltAtCallSite = new(
        id: "AXIS0608",
        title: "AxisError rebuilt at the call site from an errors-class const",
        messageFormat: "'{0}' is a const code turned into an AxisError here; declare it as a 'public static readonly AxisError' member of '{1}' and return that member instead",
        category: StyleCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A failure is a code AND a category, and the category is what the edge renders as an HTTP status. Building the AxisError at each call site lets the same code leave one handler as a 500 and another as a 400. The errors class therefore declares the finished error — a public static readonly AxisError, or a static factory method when the code carries runtime context — and call sites return that member. A bare const string survives only for the validator helpers, which take a raw code and fix the category themselves.",
        helpLinkUri: HelpBase + "axis0608");
}
