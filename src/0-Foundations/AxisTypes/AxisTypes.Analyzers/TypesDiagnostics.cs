using Microsoft.CodeAnalysis;

namespace Axis.Analyzers;

internal static class TypesDiagnostics
{
    public const string Category = "Axis.Types";

    private const string HelpBase = "https://github.com/axis-sys/axis-framework-dotnet/rules/framework/0-foundations/axis-types/";

    // Rule: types-value-object-shape (severity: must)
    public static readonly DiagnosticDescriptor ValueObjectReadonly = new(
        id: "AXIS0200",
        title: "A [ValueObject] struct must be readonly",
        messageFormat: "The value object '{0}' is a struct but is not declared readonly; a value object is immutable — declare it as a readonly partial record struct",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A value object is immutable: its backing value is set once in the private constructor and never changes. A [ValueObject] value type that is not readonly permits mutating the backing field, breaking value semantics; the canonical shape is a readonly partial record struct.",
        helpLinkUri: HelpBase + "types-value-object-shape.yaml");
}
