using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization;

public static class AuthorizationDbInit
{
    public const string Schema = "ECOMMERCE_AUTHORIZATION";

    private static AxisTable[] Tables =>
    [
        MembersTable.Define(),
    ];

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect sqlDialect) =>
    [
        ("V1", string.Join("\n", Tables.Select(table => table.Render(sqlDialect)))),
    ];
}
