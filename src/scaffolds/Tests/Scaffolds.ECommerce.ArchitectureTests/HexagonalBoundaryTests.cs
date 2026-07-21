using System.Reflection;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1;
using Scaffolds.ECommerce.Domain.Catalog.Products;
using DependencyInjection = Scaffolds.ECommerce.Application.DependencyInjection;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.ArchitectureTests;

// Executable dependency invariants (architecture-hexagonal-topology, testing-pyramid-four-projects): the
// dependency arrows point inward, with the SharedKernel as the innermost core. Reflection-only — no
// application code runs. Forbidden dependencies are asserted by referenced-assembly name, so this project
// need not reference the adapters it forbids.
public sealed class HexagonalBoundaryTests
{
    private static string[] ReferencedScaffoldAssemblies(Assembly assembly)
        => [..assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith("Scaffolds.ECommerce.", StringComparison.Ordinal))];

    [Fact]
    public void SharedKernelReferencesNoOtherScaffoldProject()
    {
        var references = ReferencedScaffoldAssemblies(typeof(ProductId).Assembly);

        Assert.Empty(references);
    }

    [Fact]
    public void DomainReferencesOnlyTheSharedKernel()
    {
        var references = ReferencedScaffoldAssemblies(typeof(IProductEntityProperties).Assembly);

        Assert.Equal("Scaffolds.ECommerce.SharedKernel", Assert.Single(references));
    }

    [Fact]
    public void ApplicationReferencesNoAdapterOrHost()
    {
        var references = ReferencedScaffoldAssemblies(typeof(DependencyInjection).Assembly);

        Assert.DoesNotContain(references, name => name.StartsWith("Scaffolds.ECommerce.Adapters", StringComparison.Ordinal));
        Assert.DoesNotContain("Scaffolds.ECommerce.Host", references);
    }

    [Fact]
    public void DrivingContractsReferenceNoDomainApplicationOrAdapter()
    {
        var references = ReferencedScaffoldAssemblies(typeof(ICatalogFacade).Assembly);

        Assert.DoesNotContain("Scaffolds.ECommerce.Domain", references);
        Assert.DoesNotContain("Scaffolds.ECommerce.Application", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Scaffolds.ECommerce.Adapters", StringComparison.Ordinal));
    }
}
