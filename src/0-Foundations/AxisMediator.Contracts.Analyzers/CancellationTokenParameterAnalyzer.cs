using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Analyzers;

// AXIS0400 — flag a CancellationToken parameter on a method of a type that implements an Axis handler
// interface. Enforces mediator-cancellation-is-ambient: the token is read from IAxisMediator, never
// received as a parameter. No-op when the mediator contracts are not referenced.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CancellationTokenParameterAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [MediatorDiagnostics.AmbientCancellation];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var handlers = MediatorHelpers.ResolveHandlerInterfaces(start.Compilation);
            if (handlers.IsEmpty)
                return; // The mediator contracts are not referenced — nothing to enforce.

            if (MediatorHelpers.IsDefinedIn(handlers[0], start.Compilation))
                return; // Compiling AxisMediator.Contracts itself.

            var cancellationToken = start.Compilation.GetTypeByMetadataName(MediatorHelpers.CancellationTokenMetadataName);
            if (cancellationToken is null)
                return;

            start.RegisterSymbolAction(ctx => Analyze(ctx, handlers, cancellationToken), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, ImmutableArray<INamedTypeSymbol> handlers, INamedTypeSymbol cancellationToken)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class)
            return;

        if (!MediatorHelpers.ImplementsAnyHandler(type, handlers))
            return;

        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method)
                continue;

            foreach (var parameter in method.Parameters)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationToken) && parameter.Locations.Length > 0)
                    context.ReportDiagnostic(Diagnostic.Create(MediatorDiagnostics.AmbientCancellation, parameter.Locations[0], method.Name));
            }
        }
    }
}
