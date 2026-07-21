using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Conventions.Analyzers;

// AXIS0600 — a class that implements an Axis CQRS handler interface must be `internal sealed`.
// Enforces rule architecture-handler-shape. Detection is SEMANTIC (implemented interface, not name), so a
// class merely named *Handler is not flagged and a handler named otherwise still is.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerAccessModifierAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ConventionsDiagnostics.HandlerInternalSealed);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var handlerInterfaces = ConventionsHelpers.HandlerInterfaceMetadataNames
                .Select(name => start.Compilation.GetTypeByMetadataName(name))
                .Where(symbol => symbol is not null)
                .Select(symbol => symbol!.OriginalDefinition)
                .ToImmutableArray();

            if (handlerInterfaces.IsEmpty)
                return; // No Axis handler interfaces referenced — nothing to enforce.

            start.RegisterSymbolAction(ctx => Analyze(ctx, handlerInterfaces), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, ImmutableArray<INamedTypeSymbol> handlerInterfaces)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic || type.IsGenericType)
            return;

        var isHandler = type.AllInterfaces.Any(implemented =>
            handlerInterfaces.Any(handler =>
                SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, handler)));

        if (!isHandler)
            return;

        if (type.DeclaredAccessibility == Accessibility.Internal && type.IsSealed)
            return;

        var location = type.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(ConventionsDiagnostics.HandlerInternalSealed, location, type.Name));
    }
}
