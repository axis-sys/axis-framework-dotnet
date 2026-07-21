using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Analyzers;

// AXIS0402 — flag a constructor parameter of type IAxisMediatorContextAccessor on a type that
// implements an Axis handler interface. Enforces mediator-ambient-context-access: a handler reads the
// ambient context through IAxisMediator, not the writable accessor. No-op when the contracts are absent.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContextAccessorInHandlerAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [MediatorDiagnostics.ContextAccessorInHandler];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var contextAccessor = start.Compilation.GetTypeByMetadataName(MediatorHelpers.ContextAccessorMetadataName);
            if (contextAccessor is null)
                return; // The mediator contracts are not referenced — nothing to enforce.

            if (MediatorHelpers.IsDefinedIn(contextAccessor, start.Compilation))
                return; // Compiling AxisMediator.Contracts itself.

            var handlers = MediatorHelpers.ResolveHandlerInterfaces(start.Compilation);
            if (handlers.IsEmpty)
                return;

            start.RegisterSymbolAction(ctx => Analyze(ctx, handlers, contextAccessor), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, ImmutableArray<INamedTypeSymbol> handlers, INamedTypeSymbol contextAccessor)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class)
            return;

        if (!MediatorHelpers.ImplementsAnyHandler(type, handlers))
            return;

        foreach (var constructor in type.InstanceConstructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter.Type.OriginalDefinition, contextAccessor) && parameter.Locations.Length > 0)
                    context.ReportDiagnostic(Diagnostic.Create(MediatorDiagnostics.ContextAccessorInHandler, parameter.Locations[0]));
            }
        }
    }
}
