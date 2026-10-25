namespace Scaffolds.ECommerce.SharedKernel.Authorization.Events.v1;

public sealed record GrantsUpdatedEvent(string CustomerId) : IAxisEvent
{
    public const string Topic = "grants_updated";
}
