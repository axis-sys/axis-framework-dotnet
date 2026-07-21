using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Axis.Analyzers;

// AXIS0004 — flag AxisResult.Try/TryAsync/TryBind/TryBindAsync called without the errorHandler
// argument. Enforces result-try-boundary: the default maps ex.Message to the error code. Detection is
// operation-based: the errorHandler argument is omitted (ArgumentKind.DefaultValue).
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TryBoundaryAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [RopDiagnostics.TryBoundary];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var axisResult = start.Compilation.GetTypeByMetadataName(RopHelpers.AxisResultMetadataName);
            if (axisResult is null)
                return; // AxisResult is not referenced here — nothing to enforce.

            if (RopHelpers.IsDefinedIn(axisResult, start.Compilation))
                return; // Compiling AxisResult itself — its internals are the sanctioned exception.

            start.RegisterOperationAction(ctx => Analyze(ctx, axisResult), OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol axisResult)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name is not ("Try" or "TryAsync" or "TryBind" or "TryBindAsync"))
            return;

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType?.OriginalDefinition, axisResult))
            return;

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name == "errorHandler" && argument.ArgumentKind == ArgumentKind.DefaultValue)
            {
                context.ReportDiagnostic(Diagnostic.Create(RopDiagnostics.TryBoundary, invocation.Syntax.GetLocation(), method.Name));
                return;
            }
        }
    }
}
