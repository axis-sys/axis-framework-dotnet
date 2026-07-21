using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Analyzers;

// AXIS0003 — flag a try/catch inside a method (or local function / accessor) that returns
// AxisResult, AxisResult<T> or Task/ValueTask thereof. Enforces rule result-no-throw: convert the
// boundary with Try/TryAsync instead of catching on the rail. A try/finally without catch is fine.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultRailTryCatchAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [RopDiagnostics.RailTryCatch];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var resultBase = start.Compilation.GetTypeByMetadataName(RopHelpers.AxisResultMetadataName);
            if (resultBase is null)
                return;

            if (RopHelpers.IsDefinedIn(resultBase, start.Compilation))
                return; // Compiling AxisResult itself — its internals are the sanctioned exception.

            var taskOfT = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
            var valueTaskOfT = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

            start.RegisterSyntaxNodeAction(
                ctx => Analyze(ctx, resultBase, taskOfT, valueTaskOfT),
                SyntaxKind.TryStatement);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol resultBase,
        INamedTypeSymbol? taskOfT,
        INamedTypeSymbol? valueTaskOfT)
    {
        var tryStatement = (TryStatementSyntax)context.Node;
        if (tryStatement.Catches.Count == 0)
            return; // try/finally without a catch is not the smell.

        var returnType = RopHelpers.GetEnclosingReturnType(tryStatement, context.SemanticModel, context.CancellationToken);
        if (returnType is null)
            return;

        if (!RopHelpers.ReturnsAxisResult(returnType, resultBase, taskOfT, valueTaskOfT))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RopDiagnostics.RailTryCatch, tryStatement.TryKeyword.GetLocation()));
    }
}
