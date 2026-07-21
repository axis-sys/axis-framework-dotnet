using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Conventions.Analyzers;

// AXIS0603 — a test method (annotated [Fact]/[Theory]) must be PascalCase (no underscore) with an Async
// suffix on async tests. Enforces rule testing-test-method-naming. Purely SYNTACTIC — no semantic model.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestMethodNamingAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ConventionsDiagnostics.TestMethodNaming);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!IsTestMethod(method))
            return;

        var name = method.Identifier.ValueText;
        var snakeCase = name.IndexOf('_') >= 0;
        var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword) || ReturnsAwaitable(method.ReturnType);
        var missingAsyncSuffix = isAsync && !name.EndsWith("Async", StringComparison.Ordinal);

        if (snakeCase || missingAsyncSuffix)
            context.ReportDiagnostic(Diagnostic.Create(ConventionsDiagnostics.TestMethodNaming, method.Identifier.GetLocation(), name));
    }

    private static bool IsTestMethod(MethodDeclarationSyntax method)
        => method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attr => attr.Name.ToString())
            .Any(n => n is "Fact" or "Theory" or "FactAttribute" or "TheoryAttribute"
                   || n.EndsWith(".Fact", StringComparison.Ordinal)
                   || n.EndsWith(".Theory", StringComparison.Ordinal));

    private static bool ReturnsAwaitable(TypeSyntax returnType)
    {
        var text = returnType.ToString();
        return text == "Task" || text == "ValueTask"
            || text.StartsWith("Task<", StringComparison.Ordinal)
            || text.StartsWith("ValueTask<", StringComparison.Ordinal)
            || text.Contains("Tasks.Task")
            || text.Contains("Tasks.ValueTask");
    }
}
