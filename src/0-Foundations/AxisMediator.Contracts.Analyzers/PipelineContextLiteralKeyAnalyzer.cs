using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Axis.Analyzers;

// AXIS0403 — flag a string-literal key passed to AxisPipelineContext.Get/Set. Enforces
// mediator-pipeline-context: keys come from AxisPipelineContextKeys constants. A reference to a named
// constant (even though it is a compile-time constant) is NOT a literal and is not flagged. No-op when
// the AxisPipelineContext type is not referenced.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PipelineContextLiteralKeyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [MediatorDiagnostics.PipelineContextLiteralKey];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var pipelineContext = start.Compilation.GetTypeByMetadataName(MediatorHelpers.PipelineContextMetadataName);
            if (pipelineContext is null)
                return; // The mediator contracts are not referenced — nothing to enforce.

            if (MediatorHelpers.IsDefinedIn(pipelineContext, start.Compilation))
                return; // Compiling AxisMediator.Contracts itself.

            start.RegisterOperationAction(ctx => Analyze(ctx, pipelineContext), OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol pipelineContext)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name is not ("Get" or "Set"))
            return;

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType?.OriginalDefinition, pipelineContext))
            return;

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name != "key")
                continue;

            if (argument.Value is ILiteralOperation { ConstantValue.HasValue: true } literal &&
                literal.Type?.SpecialType == SpecialType.System_String)
            {
                context.ReportDiagnostic(Diagnostic.Create(MediatorDiagnostics.PipelineContextLiteralKey, literal.Syntax.GetLocation(), method.Name));
            }

            return;
        }
    }
}
