using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Conventions.Analyzers;

// AXIS0604 — a concrete record/class that implements a domain properties interface (I…Properties) declared in
// the same project must be `internal sealed`. Exception: an internal non-sealed class is valid when it is the
// N2 domain-entity base an application-layer AggregateApplication inherits (domain-aggregate-levels). That
// subclass lives in a DOWNSTREAM project (Application references Domain, never the reverse), so a
// DiagnosticAnalyzer — which only ever sees ONE project's compilation — can never observe it directly; walking
// this compilation's own type graph for a derived type is a false-negative-by-construction (it only "works" in
// a single-file unit test). Instead the exception checks the one thing decidable from THIS compilation alone:
// the type declares a `protected`/`protected internal` member. That accessibility is the sole way a Rules
// method can be reachable from a subclass in another assembly (`internal` is invisible there, `public` would
// leak Rules to the whole API), so its presence is the structural precondition for the inheritance pattern to
// work at all — not a proxy for it. Enforces rule domain-properties-contract-pair. Detection of the interface
// itself is SEMANTIC (implemented interface + same assembly, not name): value objects (record structs) and
// responses that implement no such interface are not flagged; a repository's DbEntity implements the interface
// from another assembly and is out of scope.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainPropertiesRecordAccessibilityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ConventionsDiagnostics.DomainPropertiesRecordInternalSealed);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            // Anchor on the Axis result type — absent it, this is not an Axis project, so enforce nothing.
            if (start.Compilation.GetTypeByMetadataName(ConventionsHelpers.AxisErrorMetadataName) is null)
                return;

            start.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!ConventionsHelpers.IsInspectableClass(type))
            return;

        var contract = type.AllInterfaces.FirstOrDefault(
            iface => ConventionsHelpers.IsDomainPropertiesInterface(iface, type.ContainingAssembly));
        if (contract is null)
            return;

        if (type.DeclaredAccessibility != Accessibility.Internal)
        {
            var location = type.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                ConventionsDiagnostics.DomainPropertiesRecordInternalSealed, location, type.Name));
            return;
        }

        // Exception: an internal non-sealed N2 entity is valid when it exposes at least one protected member —
        // the only accessibility an application-layer AggregateApplication subclass (a different assembly) can
        // reach. No protected surface means no subclass could use this as a base anyway, so it is either a
        // finished sealed-candidate that forgot `sealed`, or a poor Properties-only record — both must flag.
        if (type.IsSealed || HasProtectedMember(type))
            return;

        var loc = type.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            ConventionsDiagnostics.DomainPropertiesRecordInternalSealed, loc, type.Name));
    }

    private static bool HasProtectedMember(INamedTypeSymbol type)
        => type.GetMembers().Any(member =>
            !member.IsImplicitlyDeclared &&
            member.DeclaredAccessibility is Accessibility.Protected or Accessibility.ProtectedOrInternal);
}
