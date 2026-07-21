using System.Collections.Concurrent;

namespace Scaffolds.ECommerce.Adapters.Driven.InMemory.Auth;

// Captures every "sent" email so a dev run (or an end-to-end test) can read what would have gone out.
public sealed class InMemoryEmailOutbox
{
    public ConcurrentQueue<AxisEmailData> Sent { get; } = new();
}
