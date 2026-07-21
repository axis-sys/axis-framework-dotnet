using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Axis.Analyzers;

// AXIS0300 — flag an IAxisBus.PublishAsync call that comes AFTER the last SaveChangesAsync of the
// enclosing member. Enforces rule bus-outbox-enqueue-in-uow-transaction: publishing enqueues the
// event on the unit of work and the commit drains it, so a publish after the commit strands the
// event outside the transaction (the outbox never sees it).
//
// Detection is textual-order within the member: in both statement sequences and fluent ROP chains
// (`...ThenAsync(unitOfWork.SaveChangesAsync)`), source order mirrors execution order. The publish
// is matched SEMANTICALLY (the method must be Axis.IAxisBus.PublishAsync); the save is matched by
// NAME (`SaveChangesAsync`, invocation or method group), because clients typically wrap the unit
// of work behind their own port and only the conventional name survives the wrapping. A member
// with no SaveChangesAsync at all is never flagged — the commit may legitimately live upstream.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PublishAfterSaveChangesAnalyzer : DiagnosticAnalyzer
{
    private const string BusMetadataName = "Axis.IAxisBus";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [BusDiagnostics.PublishAfterSave];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var busInterface = start.Compilation.GetTypeByMetadataName(BusMetadataName);
            if (busInterface is null)
                return; // AxisBus is not referenced here — nothing to enforce.

            if (SymbolEqualityComparer.Default.Equals(busInterface.ContainingAssembly, start.Compilation.Assembly))
                return; // Compiling AxisBus itself — its internals are the sanctioned exception.

            start.RegisterSyntaxNodeAction(ctx => Analyze(ctx, busInterface), SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol busInterface)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return;
        if (access.Name.Identifier.ValueText != "PublishAsync")
            return;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
            return;
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, busInterface))
            return;

        var member = invocation.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        if (member is null)
            return;

        var lastSave = member.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(name => name.Identifier.ValueText == "SaveChangesAsync")
            .Select(name => (SyntaxNode?)name)
            .LastOrDefault();
        if (lastSave is null)
            return; // No commit in this member — it may legitimately live upstream.

        if (invocation.SpanStart <= lastSave.SpanStart)
            return; // A commit still follows the publish; the queue is drained.

        context.ReportDiagnostic(Diagnostic.Create(
            BusDiagnostics.PublishAfterSave, access.Name.GetLocation()));
    }
}
