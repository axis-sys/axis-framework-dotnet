using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Customers;

public static class CustomersDbInit
{
    public const string Schema = "ECOMMERCE_CUSTOMERS";

    private static AxisTable[] Tables =>
    [
        CustomersTable.Define(),
    ];

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect sqlDialect) =>
    [
        ("V1", string.Join("\n", Tables.Select(table => table.Render(sqlDialect)))),
    ];
}
