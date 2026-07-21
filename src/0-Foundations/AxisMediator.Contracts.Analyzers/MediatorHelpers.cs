using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Axis.Analyzers;

internal static class MediatorHelpers
{
    public const string MediatorMetadataName = "AxisMediator.Contracts.IAxisMediator";
    public const string ContextAccessorMetadataName = "AxisMediator.Contracts.IAxisMediatorContextAccessor";
    public const string PipelineContextMetadataName = "AxisMediator.Contracts.Pipelines.AxisPipelineContext";
    public const string CancellationTokenMetadataName = "System.Threading.CancellationToken";

    // The five Axis handler interfaces a consumer type may implement, by metadata name (backtick arity).
    private static readonly string[] HandlerMetadataNames =
    [
        "AxisMediator.Contracts.CQRS.Commands.IAxisCommandHandler`1",
        "AxisMediator.Contracts.CQRS.Commands.IAxisCommandHandler`2",
        "AxisMediator.Contracts.CQRS.Queries.IAxisQueryHandler`2",
        "AxisMediator.Contracts.CQRS.Events.IAxisEventHandler`1",
        "AxisMediator.Contracts.CQRS.Queries.IAxisStreamQueryHandler`2",
    ];

    // Resolves whichever handler interfaces are present in the compilation. Empty when the mediator
    // contracts are not referenced — the caller then treats the analyzer as a no-op.
    public static ImmutableArray<INamedTypeSymbol> ResolveHandlerInterfaces(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var name in HandlerMetadataNames)
        {
            var symbol = compilation.GetTypeByMetadataName(name);
            if (symbol is not null)
                builder.Add(symbol);
        }

        return builder.ToImmutable();
    }

    // True when `type` implements any of the Axis handler interfaces (arity-insensitive via the open
    // generic definition).
    public static bool ImplementsAnyHandler(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> handlers)
    {
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var handler in handlers)
            {
                if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, handler))
                    return true;
            }
        }

        return false;
    }

    // True when `type` is DEFINED in `compilation` (not merely referenced). Used for auto-exemption:
    // when AxisMediator.Contracts itself is being compiled, the analyzer must not run over its own
    // declarations. Name-independent — it recognizes the assembly by ownership, not by a hardcoded name.
    public static bool IsDefinedIn(INamedTypeSymbol type, Compilation compilation)
        => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly);
}
