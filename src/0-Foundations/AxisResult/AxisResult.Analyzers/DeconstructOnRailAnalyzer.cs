using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Analyzers;

// AXIS0006 — flag leaving the rail through the positional Deconstruct (or a positional pattern /
// switch over the result) INSIDE a method that itself returns AxisResult or Task/ValueTask of it.
// Enforces rule result-deconstruct-terminal-only: the Deconstruct is the sanctioned exit at the
// TERMINAL edge; mid-rail it launders `if (result.IsFailure) return` through a tuple. The message
// inspects how the deconstructed flag is consumed and suggests the operator that fits.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeconstructOnRailAnalyzer : DiagnosticAnalyzer
{
    private const string DefaultSuggestion =
        "compose the step with Then/Map/Ensure, or collapse the outcome with Match at the terminal";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [RopDiagnostics.DeconstructOnRail];

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
                ctx => AnalyzeDeconstructionAssignment(ctx, resultBase, taskOfT, valueTaskOfT),
                SyntaxKind.SimpleAssignmentExpression);
            start.RegisterSyntaxNodeAction(
                ctx => AnalyzePositionalPattern(ctx, resultBase, taskOfT, valueTaskOfT),
                SyntaxKind.IsPatternExpression);
            start.RegisterSyntaxNodeAction(
                ctx => AnalyzeSwitch(ctx, resultBase, taskOfT, valueTaskOfT, ((SwitchStatementSyntax)ctx.Node).Expression),
                SyntaxKind.SwitchStatement);
            start.RegisterSyntaxNodeAction(
                ctx => AnalyzeSwitch(ctx, resultBase, taskOfT, valueTaskOfT, ((SwitchExpressionSyntax)ctx.Node).GoverningExpression),
                SyntaxKind.SwitchExpression);
        });
    }

    // `var (ok, value, errors) = result` / `(var ok, var value, var errors) = result` on the rail.
    private static void AnalyzeDeconstructionAssignment(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol resultBase,
        INamedTypeSymbol? taskOfT,
        INamedTypeSymbol? valueTaskOfT)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        var isDeconstruction =
            assignment.Left is DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax } ||
            assignment.Left is TupleExpressionSyntax;
        if (!isDeconstruction)
            return;

        if (!RopHelpers.InheritsFromOrEquals(context.SemanticModel.GetTypeInfo(assignment.Right, context.CancellationToken).Type, resultBase))
            return;

        if (!IsOnRail(assignment, context, resultBase, taskOfT, valueTaskOfT))
            return;

        var suggestion = SuggestOperator(assignment);
        context.ReportDiagnostic(Diagnostic.Create(
            RopDiagnostics.DeconstructOnRail, assignment.Left.GetLocation(), suggestion));
    }

    // `result is (true, var value, _)` on the rail — the pattern form of the same exit.
    private static void AnalyzePositionalPattern(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol resultBase,
        INamedTypeSymbol? taskOfT,
        INamedTypeSymbol? valueTaskOfT)
    {
        var isPattern = (IsPatternExpressionSyntax)context.Node;
        if (!ContainsPositionalPattern(isPattern.Pattern))
            return;

        if (!RopHelpers.InheritsFromOrEquals(context.SemanticModel.GetTypeInfo(isPattern.Expression, context.CancellationToken).Type, resultBase))
            return;

        if (!IsOnRail(isPattern, context, resultBase, taskOfT, valueTaskOfT))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            RopDiagnostics.DeconstructOnRail, isPattern.Pattern.GetLocation(),
            "test the value with Ensure, or collapse the outcome with Match at the terminal"));
    }

    // `result switch { (true, var v, _) => ..., ... }` on the rail — a hand-rolled Match.
    private static void AnalyzeSwitch(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol resultBase,
        INamedTypeSymbol? taskOfT,
        INamedTypeSymbol? valueTaskOfT,
        ExpressionSyntax governing)
    {
        if (!RopHelpers.InheritsFromOrEquals(context.SemanticModel.GetTypeInfo(governing, context.CancellationToken).Type, resultBase))
            return;

        // Only the positional (deconstructing) arms are this diagnostic; property-pattern branching
        // on IsSuccess/IsFailure belongs to AXIS0002, and IsTransientFailure arms are sanctioned.
        var hasPositionalArm = context.Node
            .DescendantNodes()
            .OfType<PositionalPatternClauseSyntax>()
            .Any();
        if (!hasPositionalArm)
            return;

        if (!IsOnRail(context.Node, context, resultBase, taskOfT, valueTaskOfT))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            RopDiagnostics.DeconstructOnRail, governing.GetLocation(),
            "this switch re-implements Match — collapse the outcome with Match/MatchAsync"));
    }

    private static bool ContainsPositionalPattern(PatternSyntax pattern)
        => pattern is RecursivePatternSyntax { PositionalPatternClause: not null } ||
           pattern.DescendantNodes().OfType<PositionalPatternClauseSyntax>().Any();

    private static bool IsOnRail(
        SyntaxNode node,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol resultBase,
        INamedTypeSymbol? taskOfT,
        INamedTypeSymbol? valueTaskOfT)
    {
        var returnType = RopHelpers.GetEnclosingReturnType(node, context.SemanticModel, context.CancellationToken);
        return returnType is not null && RopHelpers.ReturnsAxisResult(returnType, resultBase, taskOfT, valueTaskOfT);
    }

    // Semantic hint: look at how the success flag is consumed after the deconstruction and name the
    // operator that replaces the branch. Heuristic on the enclosing block; falls back to the menu.
    private static string SuggestOperator(AssignmentExpressionSyntax assignment)
    {
        var flagName = GetSuccessComponentName(assignment.Left);
        if (flagName is null)
            return DefaultSuggestion;

        var block = assignment.FirstAncestorOrSelf<BlockSyntax>();
        if (block is null)
            return DefaultSuggestion;

        foreach (var statement in block.Statements.Where(s => s.SpanStart > assignment.Span.End))
        {
            switch (statement)
            {
                case IfStatementSyntax ifStatement when References(ifStatement.Condition, flagName):
                    if (ifStatement.Statement.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                        .Any(i => i.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: var n } && n.StartsWith("Log", StringComparison.Ordinal)))
                        return "route the failure side effect through TapError/LogIfFailure and keep the chain";
                    if (ifStatement.Statement.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>().Any())
                        return "this guard re-implements the short-circuit — chain the step with Then/ThenAsync";
                    return DefaultSuggestion;

                case ReturnStatementSyntax { Expression: ConditionalExpressionSyntax ternary } when References(ternary.Condition, flagName):
                    return "the two arms are a hand-rolled Match — collapse with Match/MatchAsync";
            }
        }

        return DefaultSuggestion;
    }

    // Name of the first (isSuccess) component of the deconstruction target, or null for a discard.
    private static string? GetSuccessComponentName(ExpressionSyntax left)
    {
        switch (left)
        {
            case DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax parenthesized }
                when parenthesized.Variables.Count > 0 &&
                     parenthesized.Variables[0] is SingleVariableDesignationSyntax first:
                return first.Identifier.ValueText;
            case TupleExpressionSyntax tuple
                when tuple.Arguments.Count > 0 &&
                     tuple.Arguments[0].Expression is DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax first }:
                return first.Identifier.ValueText;
            default:
                return null;
        }
    }

    private static bool References(ExpressionSyntax expression, string identifier)
        => expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.ValueText == identifier);
}
