namespace Scaffolds.ECommerce.Adapters.Driven.InMemory.EmailValidation;

internal sealed class InMemoryEmailService(InMemoryEmailOutbox outbox) : IAxisEmailService
{
    public Task<AxisResult> SendAsync(AxisEmailData data)
    {
        outbox.Sent.Enqueue(data);
        return AxisResult.Ok().AsTaskAsync();
    }
}
