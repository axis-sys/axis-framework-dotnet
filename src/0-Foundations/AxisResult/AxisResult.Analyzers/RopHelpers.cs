using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Axis.Analyzers;

internal static class RopHelpers
{
    public const string AxisResultMetadataName = "Axis.AxisResult";
    public const string AxisResultOfTMetadataName = "Axis.AxisResult`1";
    public const string AxisErrorMetadataName = "Axis.AxisError";
    public const string ExceptionMetadataName = "System.Exception";

    // True when `type` is DEFINED in `compilation` (not merely referenced). Used for auto-exemption:
    // when the AxisResult primitive itself is being compiled, its internals legitimately use
    // .Value / IsSuccess / throw, so the analyzer must skip them. Name-independent: it recognizes the
    // framework assembly by the fact that it OWNS the type, so no assembly name is hardcoded.
    public static bool IsDefinedIn(INamedTypeSymbol type, Compilation compilation)
        => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly);

    // True when `type` is `target` or derives from it (walking the base-type chain, arity-insensitive).
    public static bool InheritsFromOrEquals(ITypeSymbol? type, INamedTypeSymbol target)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target))
                return true;
        }

        return false;
    }

    // Return type of the method/local function/accessor lexically enclosing `node`; null inside a
    // lambda (anonymous-function bodies are out of scope for the rail-position diagnostics).
    public static ITypeSymbol? GetEnclosingReturnType(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax method:
                    return (model.GetDeclaredSymbol(method, cancellationToken) as IMethodSymbol)?.ReturnType;
                case LocalFunctionStatementSyntax localFunction:
                    return (model.GetDeclaredSymbol(localFunction, cancellationToken) as IMethodSymbol)?.ReturnType;
                case AccessorDeclarationSyntax accessor:
                    return (model.GetDeclaredSymbol(accessor, cancellationToken) as IMethodSymbol)?.ReturnType;
                case AnonymousFunctionExpressionSyntax:
                    return null;
            }
        }

        return null;
    }

    // True when `type` is AxisResult/AxisResult<T> (or a subclass), optionally wrapped in Task<>/ValueTask<>.
    public static bool ReturnsAxisResult(
        ITypeSymbol type,
        INamedTypeSymbol resultBase,
        INamedTypeSymbol? taskOfT,
        INamedTypeSymbol? valueTaskOfT)
    {
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            var definition = named.ConstructedFrom;
            var isAwaitable =
                (taskOfT is not null && SymbolEqualityComparer.Default.Equals(definition, taskOfT)) ||
                (valueTaskOfT is not null && SymbolEqualityComparer.Default.Equals(definition, valueTaskOfT));

            if (isAwaitable)
                return ReturnsAxisResult(named.TypeArguments[0], resultBase, taskOfT, valueTaskOfT);
        }

        return InheritsFromOrEquals(type, resultBase);
    }

    // True when the node sits inside a `nameof(...)` argument, where a member reference is not a real read.
    public static bool IsInNameOf(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } })
                return true;

            if (current is StatementSyntax or MemberDeclarationSyntax)
                break;
        }

        return false;
    }
}
