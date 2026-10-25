using Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;
using Scaffolds.ECommerce.Domain.Authorization.Members;

namespace Scaffolds.ECommerce.Application.Authorization.Services.GrantsCache;

internal sealed class GrantsCacheService(
    IAxisCache cache,
    ICustomersFacade customersFacade,
    IMembersReaderPort membersReader
) : IGrantsCacheService
{
    private const string KeyPrefix = "scaffolds-ecommerce.authorization.grants.v1";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public Task<AxisResult<Grants>> GetAsync(CustomerId customerId)
        => cache.GetOrCreateAsync(KeyOf(customerId), () => ComposeAsync(customerId), Ttl);

    public Task<AxisResult> EvictAsync(CustomerId customerId)
        => cache.RemoveAsync(KeyOf(customerId));

    private Task<AxisResult<Grants>> ComposeAsync(CustomerId customerId)
        => customersFacade
            .GetCustomerAsync(new GetCustomerQuery { CustomerId = customerId })
            .ThenAsync(customer => membersReader
                .GetByCustomerIdAsync(customerId)
                .ElseNotFoundAsync(member => GrantsOf(customer, member), () => GrantsOf(customer, member: null)));

    private static string KeyOf(CustomerId customerId) => $"{KeyPrefix}.{customerId}";

    private static Grants GrantsOf(GetCustomerResponse customer, IMemberEntityProperties? member)
        => new(
            customer.CustomerId,
            customer.IsAdmin,
            IsMember: member is not null,
            IsDisabled: member?.Disabled ?? false,
            Permissions: member?.Permissions ?? []);
}
