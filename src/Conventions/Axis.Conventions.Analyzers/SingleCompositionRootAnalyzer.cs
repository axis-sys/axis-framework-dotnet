using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Conventions.Analyzers;

// AXIS0607 — at most one PUBLIC class literally named DependencyInjection per assembly.
// Enforces rule architecture-di-registration: a project's composition surface is a single public
// door. A per-bounded-context/feature decomposition (architecture-one-folder-per-feature: "each
// folder level that registers services has exactly one DependencyInjection") is allowed and
// expected, but every one of those nested classes must stay internal — only the project's root
// DependencyInjection may be public, since that is the only one another project ever calls.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleCompositionRootAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ConventionsDiagnostics.MultiplePublicCompositionExtensions);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var found = new ConcurrentBag<INamedTypeSymbol>();

            start.RegisterSymbolAction(ctx =>
            {
                var type = (INamedTypeSymbol)ctx.Symbol;

                if (type.TypeKind != TypeKind.Class)
                    return;
                if (type.Name != "DependencyInjection")
                    return;
                if (type.DeclaredAccessibility != Accessibility.Public)
                    return;
                if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, start.Compilation.Assembly))
                    return;

                found.Add(type);
            }, SymbolKind.NamedType);

            start.RegisterCompilationEndAction(end =>
            {
                if (found.Count <= 1)
                    return;

                foreach (var type in found)
                {
                    var location = type.Locations.FirstOrDefault() ?? Location.None;
                    end.ReportDiagnostic(Diagnostic.Create(
                        ConventionsDiagnostics.MultiplePublicCompositionExtensions,
                        location,
                        type.ToDisplayString(),
                        found.Count));
                }
            });
        });
    }
}
