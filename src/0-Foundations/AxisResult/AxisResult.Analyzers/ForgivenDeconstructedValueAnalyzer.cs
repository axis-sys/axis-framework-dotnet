using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Analyzers;

// AXIS0007 — flag the null-forgiving operator (`!`) applied to the VALUE component of an AxisResult<T>
// deconstruction when success was never proven. Enforces rule result-value-access-safety: the
// Deconstruct yields default(T) on failure, so `value!` without a prior read of the success flag is
// `.Value` in disguise — the throw merely deferred to a NullReferenceException. Heuristic proof: any
// reference to the success component between the deconstruction and the `!` counts as a guard; a
// discarded success component (`var (_, value, _)`) can never be proven and is always flagged.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForgivenDeconstructedValueAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [RopDiagnostics.ForgivenDeconstructedValue];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var resultOfT = start.Compilation.GetTypeByMetadataName(RopHelpers.AxisResultOfTMetadataName);
            if (resultOfT is null)
                return;

            if (RopHelpers.IsDefinedIn(resultOfT, start.Compilation))
                return; // Compiling AxisResult itself — its internals are the sanctioned exception.

            start.RegisterSyntaxNodeAction(ctx => Analyze(ctx, resultOfT), SyntaxKind.SuppressNullableWarningExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol resultOfT)
    {
        var suppress = (PostfixUnaryExpressionSyntax)context.Node;
        if (suppress.Operand is not IdentifierNameSyntax identifier)
            return;

        if (context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not ILocalSymbol local)
            return;
        if (local.DeclaringSyntaxReferences.Length != 1)
            return;
        if (local.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) is not SingleVariableDesignationSyntax designation)
            return;

        var (assignment, successDesignation) = ResolveResultDeconstruction(designation, context, resultOfT);
        if (assignment is null)
            return;

        // A discarded success flag can never prove anything — flag immediately.
        if (successDesignation is not null && IsSuccessReferencedBetween(
                successDesignation, assignment, suppress, context))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            RopDiagnostics.ForgivenDeconstructedValue, suppress.GetLocation(), identifier.Identifier.ValueText));
    }

    // When `designation` is the VALUE component (middle of three) of a deconstruction whose source is
    // AxisResult<T>, returns the assignment plus the success-component designation (null if discarded).
    private static (AssignmentExpressionSyntax? assignment, SingleVariableDesignationSyntax? success)
        ResolveResultDeconstruction(
            SingleVariableDesignationSyntax designation,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol resultOfT)
    {
        AssignmentExpressionSyntax? assignment;
        SingleVariableDesignationSyntax? success;

        switch (designation.Parent)
        {
            // var (ok, value, errors) = result
            case ParenthesizedVariableDesignationSyntax parenthesized
                when parenthesized is { Variables.Count: 3 } &&
                     parenthesized.Variables[1] == designation &&
                     parenthesized.Parent is DeclarationExpressionSyntax { Parent: AssignmentExpressionSyntax outer } &&
                     outer.Left == parenthesized.Parent:
                assignment = outer;
                success = parenthesized.Variables[0] as SingleVariableDesignationSyntax;
                break;

            // (var ok, var value, var errors) = result
            case DeclarationExpressionSyntax
            {
                Parent: ArgumentSyntax { Parent: TupleExpressionSyntax { Arguments.Count: 3 } tuple }
            } declaration
                when tuple.Arguments[1].Expression == declaration &&
                     tuple.Parent is AssignmentExpressionSyntax outer && outer.Left == tuple:
                assignment = outer;
                success = (tuple.Arguments[0].Expression as DeclarationExpressionSyntax)?.Designation
                    as SingleVariableDesignationSyntax;
                break;

            default:
                return (null, null);
        }

        var sourceType = context.SemanticModel.GetTypeInfo(assignment.Right, context.CancellationToken).Type;
        return RopHelpers.InheritsFromOrEquals(sourceType, resultOfT)
            ? (assignment, success)
            : (null, null);
    }

    // True when the success component is read anywhere between the deconstruction and the `!` —
    // the guard that proves the value is safe to forgive.
    private static bool IsSuccessReferencedBetween(
        SingleVariableDesignationSyntax successDesignation,
        AssignmentExpressionSyntax assignment,
        PostfixUnaryExpressionSyntax suppress,
        SyntaxNodeAnalysisContext context)
    {
        var successSymbol = context.SemanticModel.GetDeclaredSymbol(successDesignation, context.CancellationToken);
        if (successSymbol is null)
            return false;

        var scope = (SyntaxNode?)assignment.FirstAncestorOrSelf<MemberDeclarationSyntax>()
            ?? assignment.FirstAncestorOrSelf<BlockSyntax>();
        if (scope is null)
            return false;

        var name = successDesignation.Identifier.ValueText;
        return scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.ValueText == name)
            .Where(id => id.SpanStart > assignment.Span.End && id.SpanStart < suppress.SpanStart)
            .Any(id => SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(id, context.CancellationToken).Symbol, successSymbol));
    }
}
