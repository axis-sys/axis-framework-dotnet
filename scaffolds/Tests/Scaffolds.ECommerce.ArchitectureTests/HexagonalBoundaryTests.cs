using System.Reflection;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Domain.Catalog.Products;
using DependencyInjection = Scaffolds.ECommerce.Application.DependencyInjection;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.ArchitectureTests;

public sealed class HexagonalBoundaryTests
{
    private static string[] ReferencedScaffoldAssemblies(Assembly assembly)
        => [..assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith("Scaffolds.ECommerce.", StringComparison.Ordinal))];

    [Fact]
    public void ReferencesNoOtherScaffoldProjectWhenScanningTheSharedKernel()
    {
        var references = ReferencedScaffoldAssemblies(typeof(ProductId).Assembly);

        Assert.Empty(references);
    }

    [Fact]
    public void ReferencesOnlyTheSharedKernelWhenScanningTheDomain()
    {
        var references = ReferencedScaffoldAssemblies(typeof(IProductEntityProperties).Assembly);

        Assert.Equal("Scaffolds.ECommerce.SharedKernel", Assert.Single(references));
    }

    [Fact]
    public void ReferencesNoAdapterOrHostWhenScanningTheApplication()
    {
        var references = ReferencedScaffoldAssemblies(typeof(DependencyInjection).Assembly);

        Assert.DoesNotContain(references, name => name.StartsWith("Scaffolds.ECommerce.Adapters", StringComparison.Ordinal));
        Assert.DoesNotContain("Scaffolds.ECommerce.Host", references);
    }

    [Fact]
    public void ReferencesNoDomainApplicationOrAdapterWhenScanningTheDrivingContracts()
    {
        var references = ReferencedScaffoldAssemblies(typeof(IProductsFacade).Assembly);

        Assert.DoesNotContain("Scaffolds.ECommerce.Domain", references);
        Assert.DoesNotContain("Scaffolds.ECommerce.Application", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Scaffolds.ECommerce.Adapters", StringComparison.Ordinal));
    }
}
