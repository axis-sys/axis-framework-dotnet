using Scaffolds.ECommerce.Adapters.Driven.Repository.Auth.ValidationCodes;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Orders;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Products;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository;

public static class EComDbInit
{
    public const string Schema = "ECOMMERCE_CATALOG";

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect sqlDialect) =>
    [
        ("V1", ProductsTable.Define().Render(sqlDialect)),
        ("V2", OrdersTable.Define().Render(sqlDialect)),
        ("V3", CustomersTable.Define().Render(sqlDialect)),
        ("V4", CartItemsTable.Define().Render(sqlDialect)),
        ("V5", ValidationCodesTable.Define().Render(sqlDialect)),
    ];
}
