using System.Data.Common;
using Scaffolds.ECommerce.Domain.Authorization.Members;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;

internal sealed record MemberDbEntity(
    CustomerId CustomerId,
    bool Disabled,
    IReadOnlyList<string> Permissions
) : IMemberEntityProperties
{
    internal static MemberDbEntity FromReader(DbDataReader reader)
        => new(
            (CustomerId)reader.GetString(0),
            reader.GetBoolean(1),
            AxisJsonColumn.ReadStrings(reader.GetString(2))
        );
}
