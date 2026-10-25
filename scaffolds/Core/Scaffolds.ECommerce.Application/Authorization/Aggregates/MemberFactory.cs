using Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;
using Scaffolds.ECommerce.Domain.Authorization.Members;

namespace Scaffolds.ECommerce.Application.Authorization.Aggregates;

internal interface IMemberFactory
{
    sealed record NewArgs
    {
        public required CustomerId CustomerId { get; init; }
        public required bool Disabled { get; init; }
        public required IReadOnlyList<string> Permissions { get; init; }
    }

    Task<AxisResult<IMemberEntityProperties>> CreateAsync(NewArgs args);
}

internal sealed class MemberFactory(IMembersWritePort members) : IMemberFactory
{
    public Task<AxisResult<IMemberEntityProperties>> CreateAsync(IMemberFactory.NewArgs args)
        => CreateOrReplaceAsync(new MemberProperties(
            args.CustomerId,
            args.Disabled,
            [.. args.Permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]));

    private Task<AxisResult<IMemberEntityProperties>> CreateOrReplaceAsync(IMemberEntityProperties member)
        => members
            .CreateAsync(member)
            .RecoverConflictAsync(() => members.UpdateAsync(member));
}
