using System.Collections.Concurrent;

namespace Scaffolds.ECommerce.Adapters.Driven.InMemory.EmailValidation;

internal sealed class InMemoryEmailOutbox
{
    public ConcurrentQueue<AxisEmailData> Sent { get; } = new();
}
