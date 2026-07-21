using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Axis.Conventions.Analyzers;

// AXIS0605 — in `command with { Prop = x }` where the target is an Axis command/query (IAxisRequest) and `x` is
// a parameter of the enclosing method (a route/query-bound value), the property `Prop` must be [JsonIgnore] so
// the request body cannot also supply it. Enforces rule edge-route-injected-command-jsonignore. Detection is
// SEMANTIC (the `with` operation over an IAxisRequest whose value operand is a parameter reference).
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RouteInjectedCommandJsonIgnoreAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ConventionsDiagnostics.RouteInjectedCommandJsonIgnore);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var request = start.Compilation.GetTypeByMetadataName(ConventionsHelpers.RequestMetadataName);
            var jsonIgnore = start.Compilation.GetTypeByMetadataName(ConventionsHelpers.JsonIgnoreMetadataName);
            if (request is null || jsonIgnore is null)
                return; // Not an Axis edge project (no command contracts / no System.Text.Json) — nothing to enforce.

            start.RegisterOperationAction(ctx => Analyze(ctx, request, jsonIgnore), OperationKind.With);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol request, INamedTypeSymbol jsonIgnore)
    {
        var with = (IWithOperation)context.Operation;

        if (with.Type is not INamedTypeSymbol target || !ConventionsHelpers.Implements(target, request))
            return;

        foreach (var initializer in with.Initializer.Initializers)
        {
            if (initializer is not ISimpleAssignmentOperation assignment)
                continue;
            if (assignment.Target is not IPropertyReferenceOperation propertyReference)
                continue;
            if (!IsParameterReference(assignment.Value))
                continue;

            var property = propertyReference.Property;
            if (HasAttribute(property, jsonIgnore))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                ConventionsDiagnostics.RouteInjectedCommandJsonIgnore,
                assignment.Syntax.GetLocation(),
                property.Name));
        }
    }

    // The injected value comes straight from a method parameter — unwrap implicit conversions (e.g. a value-object
    // cast) to reach it. A local, field or literal is not route-bound, so it never trips the rule.
    private static bool IsParameterReference(IOperation value)
    {
        while (value is IConversionOperation conversion)
            value = conversion.Operand;
        return value is IParameterReferenceOperation;
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attribute)
        => symbol.GetAttributes().Any(
            data => SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute));
}
