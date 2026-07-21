using AxisMediator.Contracts;
using AxisValidator;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace AxisMediator.UnitTests.CQRS;

public class BaseUnitTest
{
    protected static IServiceProvider DefaultServiceProvider()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var serviceProvider = ServiceCollectionServiceExtensions.AddSingleton(new ServiceCollection()
                .AddCqrsMediator(assembly)
                .AddAxisValidator(assembly)
                .AddAxisLogger()
                .AddLoggingBehavior(), AxisLoggerFactory.Create)
            .AddAxisMediator()
            .AddPerformanceBehavior()
            .BuildServiceProvider();

        var contextAccessor = serviceProvider.GetRequiredService<IAxisMediatorContextAccessor>();
        contextAccessor.OriginId = $"Origin-{Guid.NewGuid():N}";
        contextAccessor.AxisEntityId = AxisEntityId.New;
        return serviceProvider;
    }

}
