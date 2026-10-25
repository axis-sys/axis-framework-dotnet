using Scaffolds.ECommerce.Application.Authorization.Services.GrantsCache;
using Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;
using Scaffolds.ECommerce.SharedKernel.Authorization.Events.v1;

namespace Scaffolds.ECommerce.Application.Authorization.Events;

internal sealed class GrantsUpdatedEventHandler(
    IGrantsCacheService grantsCache,
    IAxisLogger<GrantsUpdatedEventHandler> logger
) : IAxisEventHandler<GrantsUpdatedEvent>
{
    public async Task<AxisResult> HandleAsync(GrantsUpdatedEvent @event)
    {
        var evicted = await CustomerIdOf(@event).ThenAsync(grantsCache.EvictAsync);

        logger.LogResult(GrantsUpdatedEvent.Topic, evicted);
        return AxisResult.Ok();
    }

    private static AxisResult<CustomerId> CustomerIdOf(GrantsUpdatedEvent @event)
        => CustomerId.TryParse(@event.CustomerId, out var customerId)
            ? customerId
            : MemberErrors.GrantsUpdatedEventMalformed;
}
