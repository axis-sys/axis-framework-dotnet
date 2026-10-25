using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using DependencyInjection = Scaffolds.ECommerce.Application.DependencyInjection;

namespace Scaffolds.ECommerce.ArchitectureTests;

public sealed class FacadeBoundaryTests
{
    private static readonly Assembly DrivingContracts = typeof(IProductsFacade).Assembly;
    private static readonly Assembly Application = typeof(DependencyInjection).Assembly;
    private static readonly Assembly FacadeAdapter = typeof(Adapters.Driving.Facade.DependencyInjection).Assembly;

    private static Type[] FacadeInterfaces()
        => [..DrivingContracts.GetTypes().Where(type => type.IsInterface && type.Name.EndsWith("Facade", StringComparison.Ordinal))];

    [Fact]
    public void ImplementsEveryFacadeOnceWhenScanningTheFacadeAdapter()
    {
        foreach (var facadeInterface in FacadeInterfaces())
        {
            var implementations = FacadeAdapter.GetTypes()
                .Where(type => type.IsClass && facadeInterface.IsAssignableFrom(type))
                .ToArray();

            Assert.Single(implementations);
        }
    }

    [Fact]
    public void ImplementsNoFacadeWhenScanningTheApplication()
    {
        var offenders = Application.GetTypes()
            .Where(type => type.IsClass && FacadeInterfaces().Any(facade => facade.IsAssignableFrom(type)))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ReferencesOnlyTheDrivingContractsAndTheApplicationWhenScanningTheFacadeAdapter()
    {
        string[] allowed = ["Scaffolds.ECommerce.Application", "Scaffolds.ECommerce.Contracts.Driving"];

        var references = FacadeAdapter.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith("Scaffolds.ECommerce.", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains("Scaffolds.ECommerce.Contracts.Driving", references);
        Assert.All(references, reference => Assert.Contains(reference, allowed));
    }

    [Fact]
    public void InjectsOnlyFacadesWhenScanningTheHostControllers()
    {
        var controllers = HostAssembly.Value.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        Assert.NotEmpty(controllers);
        foreach (var controller in controllers)
        {
            foreach (var constructor in controller.GetConstructors())
            {
                foreach (var parameter in constructor.GetParameters())
                    Assert.EndsWith("Facade", parameter.ParameterType.Name);
            }
        }
    }
}
