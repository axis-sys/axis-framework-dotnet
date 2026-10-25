using AxisBus.Repository.Ports;

namespace Scaffolds.ECommerce.IntegrationTests;

internal static class OutboxDispatch
{
    public static async Task<bool> DrainCleanlyAsync(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IBusDispatcher>();

        var clean = true;
        BusDispatchPass pass;
        do
        {
            pass = await dispatcher.RunOnceAsync(TestContext.Current.CancellationToken);
            clean &= pass.Clean;
        }
        while (pass.Delivered > 0);

        return clean;
    }
}
