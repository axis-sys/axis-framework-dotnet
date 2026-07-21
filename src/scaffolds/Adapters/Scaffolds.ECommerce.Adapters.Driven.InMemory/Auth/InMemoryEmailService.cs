namespace Scaffolds.ECommerce.Adapters.Driven.InMemory.Auth;

internal sealed class InMemoryEmailService(InMemoryEmailOutbox outbox) : IAxisEmailService
{
    public Task<AxisResult> SendAsync(AxisEmailData data)
    {
        outbox.Sent.Enqueue(data);
        return AxisResult.Ok().AsTaskAsync();
    }
}
