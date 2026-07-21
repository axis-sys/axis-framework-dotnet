using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Axis.Conventions.Analyzers;

// AXIS0606 — a record that implements an entity properties interface (I…Properties): a domain {Entity}Properties
// record or a repository {Entity}DbEntity. When its primary constructor declares two or more parameters, each
// parameter must be on its own line (and none on the record-declaration line), so the entity's shape reads
// vertically. Enforces rule style-entity-record-parameter-per-line. Detection is SEMANTIC on the implemented
// interface (any assembly, so DbEntity records in the adapter are in scope) and SYNTACTIC on the parameter layout.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityPropertiesRecordParameterPerLineAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ConventionsDiagnostics.EntityPropertiesRecordParameterPerLine);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            // Anchor on the Axis result type — absent it, this is not an Axis project, so enforce nothing.
            if (start.Compilation.GetTypeByMetadataName(ConventionsHelpers.AxisErrorMetadataName) is null)
                return;

            // Record classes only (SyntaxKind.RecordDeclaration); a record struct is a value object, not an entity.
            start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.RecordDeclaration);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var record = (RecordDeclarationSyntax)context.Node;

        // Only a positional record with two or more parameters can cram its shape onto shared lines.
        var parameterList = record.ParameterList;
        if (parameterList is null || parameterList.Parameters.Count < 2)
            return;

        if (context.SemanticModel.GetDeclaredSymbol(record, context.CancellationToken) is not { } type)
            return;

        if (!ConventionsHelpers.ImplementsEntityPropertiesInterface(type))
            return;

        var declarationLine = record.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line;

        var seenLines = new HashSet<int>();
        foreach (var parameter in parameterList.Parameters)
        {
            var line = parameter.GetLocation().GetLineSpan().StartLinePosition.Line;

            // A parameter sharing the record-declaration line, or sharing a line with another parameter, breaks
            // the one-per-line layout.
            if (line == declarationLine || !seenLines.Add(line))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ConventionsDiagnostics.EntityPropertiesRecordParameterPerLine,
                    parameterList.GetLocation(),
                    type.Name));
                return;
            }
        }
    }
}
