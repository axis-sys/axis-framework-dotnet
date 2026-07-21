using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

namespace Scaffolds.ECommerce.E2ETests.Fixtures;

/// <summary>
/// Proves every controller activates from the production container (the reflective
/// production-composition test required by testing-e2e-controller-coverage-gate).
/// </summary>
public static class CompositionTests
{
    public static void AllControllersActivateFromProductionContainer(ECommerceWebAppFactory factory)
    {
        var controllerTypes = typeof(Host.Program).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();
        using var scope = factory.Services.CreateScope();

        Assert.NotEmpty(controllerTypes);
        foreach (var controllerType in controllerTypes)
            Assert.NotNull(ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType));
    }
}
