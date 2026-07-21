using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace AxisTypes.Analyzers.UnitTests;

// Runs a single analyzer over an in-memory C# snippet using the running runtime's reference set,
// so tests stay pinned to the same Roslyn the framework builds with. Snippets are self-contained
// (they define the [ValueObject] attribute inline), so no AxisTypes reference is added — that would
// make GetTypeByMetadataName ambiguous with the internal generated attribute.
internal static class AnalyzerHarness
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;

        return [
            ..trusted.Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(p))
                .Select(MetadataReference (p) => MetadataReference.CreateFromFile(p))
        ];
    }

    public static async Task<ImmutableArray<Diagnostic>> RunAsync<TAnalyzer>(string source)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            assemblyName: "AxisTypesAnalyzerTestAssembly",
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
