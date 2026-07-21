using AxisMediator.Contracts;
using AxisValidator;
using FluentValidation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Axis.Conventions.Analyzers.UnitTests;

// Runs a single analyzer over an in-memory C# snippet using the running runtime's reference set plus the
// Axis assemblies the snippets need (AxisResult, AxisMediator.Contracts, AxisValidator + FluentValidation).
// ASP.NET Core (ControllerBase) arrives via the FrameworkReference, so it is already in the trusted set.
// Pinned to the same Roslyn the framework builds with. No external test harness.
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
            typeof(AxisError).Assembly.Location,                       // AxisResult
            typeof(IAxisMediator).Assembly.Location, // AxisMediator.Contracts (handler interfaces)
            typeof(AxisValidatorBase<>).Assembly.Location,    // AxisValidator.FluentValidation (base class)
            typeof(IAxisValidatorBase<>).Assembly.Location,   // AxisValidator (interface)
            typeof(AbstractValidator<>).Assembly.Location, // FluentValidation (base of the base)
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
            assemblyName: "AxisConventionsAnalyzerTestAssembly",
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
