using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Analyzers;

// AXIS0004 — flag a [ValueObject] value type that is not readonly. Enforces types-value-object-shape:
// a value object is immutable, so its struct form must be readonly. No-op when the [ValueObject]
// attribute is not in the compilation (the AxisTypes generator is not referenced).
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectReadonlyAnalyzer : DiagnosticAnalyzer
{
    private const string ValueObjectAttributeMetadataName = "AxisTypes.SourceGenerator.ValueObjectAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [TypesDiagnostics.ValueObjectReadonly];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var attribute = start.Compilation.GetTypeByMetadataName(ValueObjectAttributeMetadataName);
            if (attribute is null)
                return; // The [ValueObject] generator is not referenced here — nothing to enforce.

            start.RegisterSymbolAction(ctx => Analyze(ctx, attribute), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attribute)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!type.IsValueType || type.IsReadOnly)
            return; // reference-type value objects and readonly structs are already correct.

        if (!HasValueObjectAttribute(type, attribute))
            return;

        if (type.Locations.Length == 0)
            return;

        context.ReportDiagnostic(Diagnostic.Create(TypesDiagnostics.ValueObjectReadonly, type.Locations[0], type.Name));
    }

    private static bool HasValueObjectAttribute(INamedTypeSymbol type, INamedTypeSymbol attribute)
    {
        foreach (var data in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute))
                return true;
        }

        return false;
    }
}
