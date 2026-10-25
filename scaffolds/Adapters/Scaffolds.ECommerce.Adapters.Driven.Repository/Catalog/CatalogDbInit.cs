using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Orders;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Products;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog;

public static class CatalogDbInit
{
    public const string Schema = "ECOMMERCE_CATALOG";

    private static AxisTable[] Tables =>
    [
        ProductsTable.Define(),
        OrdersTable.Define(),
        CartItemsTable.Define(),
    ];

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect sqlDialect) =>
    [
        ("V1", string.Join("\n", Tables.Select(table => table.Render(sqlDialect)))),
    ];
}
