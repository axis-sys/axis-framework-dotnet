using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Analyzers;

// AXIS0001 — flag a bare `.Value` read on AxisResult<T> (it throws on failure). Enforces
// rule result-value-access-safety. Detection is semantic: it resolves the property symbol and
// matches AxisResult<T>, so Nullable<T>.Value, KeyValuePair.Value, etc. are never flagged.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueAccessSafetyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [RopDiagnostics.ValueAccess];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var resultOfT = start.Compilation.GetTypeByMetadataName(RopHelpers.AxisResultOfTMetadataName);
            if (resultOfT is null)
                return; // AxisResult is not referenced here — nothing to enforce.

            if (RopHelpers.IsDefinedIn(resultOfT, start.Compilation))
                return; // Compiling AxisResult itself — its internals are the sanctioned exception.

            start.RegisterSyntaxNodeAction(ctx => Analyze(ctx, resultOfT), SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol resultOfT)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (access.Name.Identifier.ValueText != "Value")
            return;

        if (RopHelpers.IsInNameOf(access))
            return;

        if (context.SemanticModel.GetSymbolInfo(access, context.CancellationToken).Symbol is not IPropertySymbol property)
            return;

        // Resolve overrides back to the declaring `Value` on AxisResult<TValue>.
        var declaring = property;
        while (declaring.OverriddenProperty is not null)
            declaring = declaring.OverriddenProperty;

        if (!SymbolEqualityComparer.Default.Equals(declaring.ContainingType?.OriginalDefinition, resultOfT))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RopDiagnostics.ValueAccess, access.Name.GetLocation()));
    }
}
