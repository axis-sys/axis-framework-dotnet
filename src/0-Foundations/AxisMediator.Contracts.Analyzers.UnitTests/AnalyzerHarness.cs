using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Reflection;

namespace AxisMediator.Contracts.Analyzers.UnitTests;

// Runs a single analyzer over an in-memory C# snippet using the running runtime's reference set plus
// AxisMediator.Contracts and its transitive framework deps (AxisResult, AxisTypes), so snippets can
// reference IAxisMediator, the handler interfaces and Task<AxisResult<T>>. No external test harness.
internal static class AnalyzerHarness
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;

        var paths = new HashSet<string>(
            trusted.Split(Path.PathSeparator).Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        var contracts = typeof(IAxisMediator).Assembly;
        paths.Add(contracts.Location);
        foreach (var name in contracts.GetReferencedAssemblies())
        {
            try
            {
                var location = Assembly.Load(name).Location;
                if (!string.IsNullOrEmpty(location))
                    paths.Add(location);
            }
            catch
            {
                // A reference that cannot be loaded is already covered by the platform set.
            }
        }

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
            assemblyName: "AxisMediatorAnalyzerTestAssembly",
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
