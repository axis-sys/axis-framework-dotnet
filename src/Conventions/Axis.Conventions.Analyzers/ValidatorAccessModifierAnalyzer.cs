using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Conventions.Analyzers;

// AXIS0601 — a class that derives AxisValidatorBase<T> must be `internal sealed`.
// Enforces rule architecture-validator-conventions. Detection is SEMANTIC (base type, not name).
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValidatorAccessModifierAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ConventionsDiagnostics.ValidatorInternalSealed);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var validatorBase =
                start.Compilation.GetTypeByMetadataName(ConventionsHelpers.ValidatorBaseMetadataName)?.OriginalDefinition;
            if (validatorBase is null)
                return;

            start.RegisterSymbolAction(ctx => Analyze(ctx, validatorBase), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol validatorBase)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!ConventionsHelpers.IsInspectableClass(type))
            return;
        if (!ConventionsHelpers.DerivesFrom(type, validatorBase))
            return;
        if (type.DeclaredAccessibility == Accessibility.Internal && type.IsSealed)
            return;

        var location = type.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(ConventionsDiagnostics.ValidatorInternalSealed, location, type.Name));
    }
}
