namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;

public static class MembersTable
{
    public const string Table = $"{AuthorizationDbInit.Schema}.MEMBERS";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(MembersColumns.CustomerId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(MembersColumns.Disabled, AxisDbType.Bool, notNull: true)
        .Column(MembersColumns.Permissions, AxisDbType.Json, notNull: true);
}
