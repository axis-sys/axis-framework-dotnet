using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Axis.Analyzers;

// AXIS0005 — flag an AxisError factory call whose code argument is an exception's Message. Enforces
// result-error-typing: the code is a stable constant, never parsed prose. Detection is semantic: the
// argument resolves to the Message property of a System.Exception-derived type.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ErrorCodeFromMessageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [RopDiagnostics.ErrorCodeFromMessage];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var axisError = start.Compilation.GetTypeByMetadataName(RopHelpers.AxisErrorMetadataName);
            var exception = start.Compilation.GetTypeByMetadataName(RopHelpers.ExceptionMetadataName);
            if (axisError is null || exception is null)
                return; // AxisError is not referenced here — nothing to enforce.

            if (RopHelpers.IsDefinedIn(axisError, start.Compilation))
                return; // Compiling AxisResult itself — its internals are the sanctioned exception.

            start.RegisterOperationAction(ctx => Analyze(ctx, axisError, exception), OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol axisError, INamedTypeSymbol exception)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (!method.IsStatic || !SymbolEqualityComparer.Default.Equals(method.ContainingType?.OriginalDefinition, axisError))
            return; // only the AxisError static factories carry the code argument.

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Value is IPropertyReferenceOperation property
                && property.Property.Name == "Message"
                && RopHelpers.InheritsFromOrEquals(property.Property.ContainingType, exception))
            {
                context.ReportDiagnostic(Diagnostic.Create(RopDiagnostics.ErrorCodeFromMessage, argument.Syntax.GetLocation()));
                return;
            }
        }
    }
}
