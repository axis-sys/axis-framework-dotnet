using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Axis.Analyzers;

// AXIS0401 — flag an access to IAxisMediator.Cqrs inside a type that implements an Axis handler
// interface. Enforces mediator-dispatch-surface: dispatch is issued from the Facade, not from a
// handler. No-op when the mediator contracts are not referenced.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MediatorDispatchInHandlerAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [MediatorDiagnostics.DispatchInHandler];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var mediator = start.Compilation.GetTypeByMetadataName(MediatorHelpers.MediatorMetadataName);
            if (mediator is null)
                return; // The mediator contracts are not referenced — nothing to enforce.

            if (MediatorHelpers.IsDefinedIn(mediator, start.Compilation))
                return; // Compiling AxisMediator.Contracts itself.

            var handlers = MediatorHelpers.ResolveHandlerInterfaces(start.Compilation);
            if (handlers.IsEmpty)
                return;

            start.RegisterOperationAction(ctx => Analyze(ctx, mediator, handlers), OperationKind.PropertyReference);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol mediator, ImmutableArray<INamedTypeSymbol> handlers)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;

        if (reference.Property.Name != "Cqrs")
            return;

        if (!SymbolEqualityComparer.Default.Equals(reference.Property.ContainingType?.OriginalDefinition, mediator))
            return;

        var containingType = context.ContainingSymbol.ContainingType;
        if (containingType is null || !MediatorHelpers.ImplementsAnyHandler(containingType, handlers))
            return;

        context.ReportDiagnostic(Diagnostic.Create(MediatorDiagnostics.DispatchInHandler, reference.Syntax.GetLocation()));
    }
}
