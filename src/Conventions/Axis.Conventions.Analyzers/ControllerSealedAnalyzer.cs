using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Conventions.Analyzers;

// AXIS0602 — a class that derives ControllerBase must be `sealed`. Enforces rule edge-controller-shape.
// Detection is SEMANTIC (base type). Abstract base controllers are exempt (they are meant to be inherited).
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ControllerSealedAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ConventionsDiagnostics.ControllerSealed);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var controllerBase =
                start.Compilation.GetTypeByMetadataName(ConventionsHelpers.ControllerBaseMetadataName)?.OriginalDefinition;
            if (controllerBase is null)
                return;

            start.RegisterSymbolAction(ctx => Analyze(ctx, controllerBase), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol controllerBase)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic)
            return;
        if (!ConventionsHelpers.DerivesFrom(type, controllerBase))
            return;
        if (type.IsSealed)
            return;

        var location = type.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(ConventionsDiagnostics.ControllerSealed, location, type.Name));
    }
}
