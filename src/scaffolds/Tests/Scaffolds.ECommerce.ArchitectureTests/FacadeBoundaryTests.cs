using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1;
using DependencyInjection = Scaffolds.ECommerce.Application.DependencyInjection;

namespace Scaffolds.ECommerce.ArchitectureTests;

// Executable invariants for the facade driving adapter (architecture-facade-pattern,
// edge-controller-facade-injection): the facade implementations live in the dedicated
// {App}.Adapters.Driving.Facade project — never in the Application — and controllers depend on
// facades only. Reflection-only; no application code runs.
public sealed class FacadeBoundaryTests
{
    private static readonly Assembly DrivingContracts = typeof(ICatalogFacade).Assembly;
    private static readonly Assembly Application = typeof(DependencyInjection).Assembly;
    private static readonly Assembly FacadeAdapter = typeof(Adapters.Driving.Facade.DependencyInjection).Assembly;
    private static readonly Assembly Host = typeof(Program).Assembly;

    private static Type[] FacadeInterfaces()
        => [..DrivingContracts.GetTypes().Where(type => type.IsInterface && type.Name.EndsWith("Facade", StringComparison.Ordinal))];

    [Fact]
    public void EveryFacadeInterfaceHasExactlyOneImplementationInTheFacadeAdapter()
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
    public void TheApplicationImplementsNoFacade()
    {
        var offenders = Application.GetTypes()
            .Where(type => type.IsClass && FacadeInterfaces().Any(facade => facade.IsAssignableFrom(type)))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheFacadeAdapterReferencesOnlyTheDrivingContractsAndTheApplication()
    {
        // The compiler drops unused references from metadata, so assert the allowed SET: nothing
        // beyond the driving contracts and the application may appear (never a driven side or the host).
        string[] allowed = ["Scaffolds.ECommerce.Application", "Scaffolds.ECommerce.Contracts.Driving"];
        var references = FacadeAdapter.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith("Scaffolds.ECommerce.", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains("Scaffolds.ECommerce.Contracts.Driving", references);
        Assert.All(references, reference => Assert.Contains(reference, allowed));
    }

    [Fact]
    public void EveryControllerConstructorParameterIsAFacade()
    {
        var controllers = Host.GetTypes()
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
