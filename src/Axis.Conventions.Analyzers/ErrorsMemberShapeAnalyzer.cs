using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Axis.Conventions.Analyzers;

// AXIS0608 — an AxisError factory call whose code argument is a `const string` member of an {X}Errors class,
// made OUTSIDE that class, rebuilds an error the errors class should already own. The code and its category are
// two halves of one failure: leaving the category to each call site lets the same code surface as a 500 here and
// a 400 there. Detection is SEMANTIC (an invocation of a static factory declared on Axis.AxisError whose argument
// resolves to a const field of a type named *Errors).
//
// A const the validator helpers consume (NotNullOrEmpty(x => x.Name, Errors.NAME_REQUIRED)) is never an argument
// to an AxisError factory, so the carve-out for validation codes falls out of the detection shape for free.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ErrorsMemberShapeAnalyzer : DiagnosticAnalyzer
{
    private const string ErrorsClassSuffix = "Errors";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ConventionsDiagnostics.ErrorsMemberRebuiltAtCallSite);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var axisError = start.Compilation.GetTypeByMetadataName(ConventionsHelpers.AxisErrorMetadataName);
            if (axisError is null)
                return; // Not an Axis project (AxisResult not referenced) — nothing to enforce.

            start.RegisterOperationAction(ctx => Analyze(ctx, axisError), OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol axisError)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (!method.IsStatic
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, axisError)
            || !SymbolEqualityComparer.Default.Equals(method.ReturnType, axisError))
            return;

        foreach (var argument in invocation.Arguments)
        {
            if (ResolveErrorsClassConst(argument.Value) is not IFieldSymbol field)
                continue;

            // Inside the errors class itself this IS the canonical declaration — the shape the convention asks for.
            if (SymbolEqualityComparer.Default.Equals(field.ContainingType, context.ContainingSymbol.ContainingType))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                ConventionsDiagnostics.ErrorsMemberRebuiltAtCallSite,
                invocation.Syntax.GetLocation(),
                field.ContainingType.Name + "." + field.Name,
                field.ContainingType.Name));
        }
    }

    // The argument is a reference to a `const string` field declared on a static class named *Errors — i.e. a code
    // whose only use is to be turned into an AxisError, which is the member that should have been declared instead.
    private static IFieldSymbol? ResolveErrorsClassConst(IOperation value)
    {
        while (value is IConversionOperation conversion)
            value = conversion.Operand;

        if (value is not IFieldReferenceOperation fieldReference)
            return null;

        var field = fieldReference.Field;
        if (!field.IsConst || field.Type.SpecialType != SpecialType.System_String)
            return null;

        var owner = field.ContainingType;
        return owner.IsStatic && owner.Name.EndsWith(ErrorsClassSuffix, StringComparison.Ordinal) ? field : null;
    }
}
