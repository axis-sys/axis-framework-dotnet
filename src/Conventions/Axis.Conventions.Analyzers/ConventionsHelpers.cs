using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Axis.Conventions.Analyzers;

internal static class ConventionsHelpers
{
    // The CQRS handler interfaces from the AxisMediator.Contracts package. The analyzer is a no-op when none
    // is in the compilation (GetTypeByMetadataName returns null), so it never fires on code that doesn't use Axis.
    public static readonly ImmutableArray<string> HandlerInterfaceMetadataNames = ImmutableArray.Create(
        "AxisMediator.Contracts.CQRS.Commands.IAxisCommandHandler`1",
        "AxisMediator.Contracts.CQRS.Commands.IAxisCommandHandler`2",
        "AxisMediator.Contracts.CQRS.Queries.IAxisQueryHandler`2",
        "AxisMediator.Contracts.CQRS.Queries.IAxisStreamQueryHandler`2",
        "AxisMediator.Contracts.CQRS.Events.IAxisEventHandler`1");

    // Resolves whichever CQRS handler interfaces are present in the compilation, by original definition.
    // Empty when the mediator contracts are not referenced — a caller then treats its analyzer as a no-op, so
    // it never fires on code that doesn't use Axis.
    public static ImmutableArray<INamedTypeSymbol> ResolveHandlerInterfaces(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var name in HandlerInterfaceMetadataNames)
        {
            var symbol = compilation.GetTypeByMetadataName(name);
            if (symbol is not null)
                builder.Add(symbol.OriginalDefinition);
        }

        return builder.ToImmutable();
    }

    public const string ValidatorBaseMetadataName = "AxisValidator.AxisValidatorBase`1";
    public const string ControllerBaseMetadataName = "Microsoft.AspNetCore.Mvc.ControllerBase";

    // Anchor for the domain-properties analyzer: the ubiquitous Axis result type. Resolving it proves the
    // compilation is an Axis project, so the analyzer stays a no-op on unrelated code.
    public const string AxisErrorMetadataName = "Axis.AxisError";
    // The shared base of every command/query — the anchor for the route-injection analyzer.
    public const string RequestMetadataName = "AxisMediator.Contracts.CQRS.IAxisRequest";
    public const string JsonIgnoreMetadataName = "System.Text.Json.Serialization.JsonIgnoreAttribute";

    // True when a concrete, non-generic class we should inspect (skip abstract/static/generic/non-class).
    public static bool IsInspectableClass(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Class && !type.IsAbstract && !type.IsStatic && !type.IsGenericType;

    // True when `type` implements any of `interfaces` (compared by original definition).
    public static bool ImplementsAny(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> interfaces)
    {
        foreach (var implemented in type.AllInterfaces)
            foreach (var target in interfaces)
                if (SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, target))
                    return true;
        return false;
    }

    // True when `type` derives from `baseDef` (walking the base chain by original definition), excluding itself.
    public static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseDef)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseDef))
                return true;
        return false;
    }

    // True when `type` implements `iface` (by original definition, walking every implemented interface).
    public static bool Implements(INamedTypeSymbol type, INamedTypeSymbol iface)
    {
        foreach (var implemented in type.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, iface.OriginalDefinition))
                return true;
        return false;
    }

    // True when `iface` is a domain properties contract — a name I…Properties declared in the SAME assembly as
    // the concrete type implementing it. That co-location is the interface/record pair of the domain project;
    // a repository's DbEntity implements the same interface from another assembly and is out of this rule's scope.
    public static bool IsDomainPropertiesInterface(INamedTypeSymbol iface, IAssemblySymbol declaringAssembly)
        => iface.TypeKind == TypeKind.Interface
           && iface.Name.StartsWith("I", StringComparison.Ordinal)
           && iface.Name.EndsWith("Properties", StringComparison.Ordinal)
           && SymbolEqualityComparer.Default.Equals(iface.ContainingAssembly, declaringAssembly);

    // True when `iface` is any entity properties contract — a name I…Properties, from ANY assembly. Unlike
    // IsDomainPropertiesInterface this drops the same-assembly restriction, so it matches both the domain's own
    // {Entity}Properties record and a repository's {Entity}DbEntity, which implement the interface from afar.
    public static bool IsEntityPropertiesInterface(INamedTypeSymbol iface)
        => iface.TypeKind == TypeKind.Interface
           && iface.Name.StartsWith("I", StringComparison.Ordinal)
           && iface.Name.EndsWith("Properties", StringComparison.Ordinal);

    // True when `type` implements any I…Properties entity contract (from any assembly).
    public static bool ImplementsEntityPropertiesInterface(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
            if (IsEntityPropertiesInterface(iface))
                return true;
        return false;
    }
}
