using AxisMediator.Contracts.CQRS.Events;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace AxisBus.Analyzers.UnitTests;

// Runs a single analyzer over an in-memory C# snippet using the running runtime's reference set,
// so tests stay pinned to the same Roslyn the framework builds with. No external test harness.
internal static class AnalyzerHarness
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;

        var paths = new HashSet<string>(
            trusted.Split(Path.PathSeparator).Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase)
        {
            typeof(IAxisBus).Assembly.Location, // ensure AxisBus itself is referenceable
            typeof(AxisError).Assembly.Location,
            typeof(IAxisEvent).Assembly.Location,
        };

        return [
            ..paths
                .Where(File.Exists)
                .Select(MetadataReference (p) => MetadataReference.CreateFromFile(p))
        ];
    }

    public static async Task<ImmutableArray<Diagnostic>> RunAsync<TAnalyzer>(string source)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            assemblyName: "AxisAnalyzerTestAssembly",
            syntaxTrees: [tree],
            references: References,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compileErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (compileErrors.Length > 0)
        {
            throw new InvalidOperationException(
                "Test snippet failed to compile:" + Environment.NewLine +
                string.Join(Environment.NewLine, compileErrors.Select(e => e.ToString())));
        }

        var withAnalyzers = compilation.WithAnalyzers([new TAnalyzer()]);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    public static int Count(this ImmutableArray<Diagnostic> diagnostics, string id)
        => diagnostics.Count(d => d.Id == id);
}
