using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Analyzers;

// AXIS0002 — flag branching on an AxisResult outcome. Enforces rule result-no-if-else-flow.
// Three spellings of the same smell are reported: (1) `IsSuccess`/`IsFailure` used directly as an
// if/ternary/loop condition; (2) the flag LAUNDERED through a bool local that later drives a branch
// (`var ok = r.IsSuccess; if (ok) ...`); (3) the property-pattern form (`r is { IsSuccess: true }`).
// Reading the flag as a plain value (an assertion, a return) is allowed; only branching is reported.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultBranchingAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(RopDiagnostics.Branching);

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

            start.RegisterSyntaxNodeAction(ctx => Analyze(ctx, resultBase), SyntaxKind.SimpleMemberAccessExpression);
            start.RegisterSyntaxNodeAction(ctx => AnalyzeSubpattern(ctx, resultBase), SyntaxKind.Subpattern);
            start.RegisterSyntaxNodeAction(
                ctx => AnalyzeLaunderedLocal(ctx, resultBase),
                SyntaxKind.IfStatement,
                SyntaxKind.WhileStatement,
                SyntaxKind.DoStatement,
                SyntaxKind.ForStatement,
                SyntaxKind.ConditionalExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol resultBase)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        var member = access.Name.Identifier.ValueText;
        // Deliberately excludes IsTransientFailure: it is a different classification axis
        // (transient-vs-terminal, for retry/poll-loop control flow) than IsSuccess/IsFailure
        // (the ROP rail). Branching on it is the sanctioned pattern — see rule
        // result-transient-failure-classification.
        if (member != "IsSuccess" && member != "IsFailure")
            return;

        if (context.SemanticModel.GetSymbolInfo(access, context.CancellationToken).Symbol is not IPropertySymbol property)
            return;
        if (property.Name != member)
            return;
        if (!RopHelpers.InheritsFromOrEquals(property.ContainingType, resultBase))
            return;

        if (!IsBranchCondition(access))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RopDiagnostics.Branching, access.Name.GetLocation(), member));
    }

    // The pattern form of the same branch: `r is { IsSuccess: true }`. A pattern match is
    // intrinsically a test, so it is reported wherever it appears; the sanctioned pattern exit is
    // the POSITIONAL one at the terminal (see result-deconstruct-terminal-only). IsTransientFailure
    // subpatterns are the sanctioned retry axis and never reported.
    private static void AnalyzeSubpattern(SyntaxNodeAnalysisContext context, INamedTypeSymbol resultBase)
    {
        var subpattern = (SubpatternSyntax)context.Node;
        var name = subpattern.ExpressionColon switch
        {
            NameColonSyntax nameColon => nameColon.Name.Identifier.ValueText,
            ExpressionColonSyntax { Expression: IdentifierNameSyntax id } => id.Identifier.ValueText,
            _ => null,
        };
        if (name != "IsSuccess" && name != "IsFailure")
            return;

        if (subpattern.Parent?.Parent is not RecursivePatternSyntax recursive)
            return;

        var typeInfo = context.SemanticModel.GetTypeInfo(recursive, context.CancellationToken);
        var inputType = typeInfo.ConvertedType ?? typeInfo.Type;
        if (!RopHelpers.InheritsFromOrEquals(inputType, resultBase))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            RopDiagnostics.Branching, subpattern.ExpressionColon!.GetLocation(), name));
    }

    // The laundered form: a bool local initialized from IsSuccess/IsFailure that later drives a
    // branch condition. The plain read stays allowed; it is the branch on the laundered flag that
    // reintroduces the manual short-circuit.
    private static void AnalyzeLaunderedLocal(SyntaxNodeAnalysisContext context, INamedTypeSymbol resultBase)
    {
        var condition = context.Node switch
        {
            IfStatementSyntax node => node.Condition,
            WhileStatementSyntax node => node.Condition,
            DoStatementSyntax node => node.Condition,
            ForStatementSyntax node => node.Condition,
            ConditionalExpressionSyntax node => node.Condition,
            _ => null,
        };
        if (condition is null)
            return;

        foreach (var identifier in condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            // A nested ternary's condition is scanned by its own registration — skip to avoid duplicates.
            if (IsInsideNestedTernaryCondition(identifier, condition, context.Node))
                continue;

            if (context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not ILocalSymbol local)
                continue;
            if (local.Type.SpecialType != SpecialType.System_Boolean)
                continue;
            if (local.DeclaringSyntaxReferences.Length != 1)
                continue;
            if (local.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken)
                is not VariableDeclaratorSyntax { Initializer.Value: MemberAccessExpressionSyntax member })
                continue;

            var memberName = member.Name.Identifier.ValueText;
            if (memberName != "IsSuccess" && memberName != "IsFailure")
                continue;
            if (context.SemanticModel.GetSymbolInfo(member, context.CancellationToken).Symbol is not IPropertySymbol property)
                continue;
            if (property.Name != memberName || !RopHelpers.InheritsFromOrEquals(property.ContainingType, resultBase))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(RopDiagnostics.Branching, identifier.GetLocation(), memberName));
        }
    }

    private static bool IsInsideNestedTernaryCondition(SyntaxNode identifier, ExpressionSyntax condition, SyntaxNode construct)
    {
        for (var ancestor = identifier.Parent; ancestor is not null && ancestor != condition.Parent; ancestor = ancestor.Parent)
        {
            if (ancestor is ConditionalExpressionSyntax nested && nested != construct && nested.Condition.Span.Contains(identifier.Span))
                return true;
        }

        return false;
    }

    // True when the expression lies within the condition of an if/ternary/while/do/for construct,
    // stopping at the nearest enclosing statement, lambda or member so body usages are not flagged.
    private static bool IsBranchCondition(ExpressionSyntax expression)
    {
        foreach (var ancestor in expression.Ancestors())
        {
            switch (ancestor)
            {
                case IfStatementSyntax node when node.Condition.Span.Contains(expression.Span):
                    return true;
                case ConditionalExpressionSyntax node when node.Condition.Span.Contains(expression.Span):
                    return true;
                case WhileStatementSyntax node when node.Condition.Span.Contains(expression.Span):
                    return true;
                case DoStatementSyntax node when node.Condition.Span.Contains(expression.Span):
                    return true;
                case ForStatementSyntax node when node.Condition is not null && node.Condition.Span.Contains(expression.Span):
                    return true;
                case AnonymousFunctionExpressionSyntax:
                case MemberDeclarationSyntax:
                case StatementSyntax:
                    return false;
            }
        }

        return false;
    }
}
