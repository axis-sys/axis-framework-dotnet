using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Products;

internal sealed class ProductsRepository(IAxisDbRepository db) : IProductsPort
{
    private const string Select = $"SELECT {ProductsColumns.All}";

    public Task<AxisResult<IProductEntityProperties>> GetByIdAsync(ProductId productId)
        => db.GetAsync<IProductEntityProperties>(
            $"{Select} FROM {ProductsTable.Table} WHERE {ProductsColumns.ProductId} = @productId",
            b => b.Add("productId", productId.ToString()),
            ProductDbEntity.FromReader,
            "PRODUCT_NOT_FOUND");

    public Task<AxisResult<IProductEntityProperties>> GetBySkuAsync(Sku sku)
        => db.GetAsync<IProductEntityProperties>(
            $"{Select} FROM {ProductsTable.Table} WHERE {ProductsColumns.Sku} = @sku",
            b => b.Add("sku", sku.ToString()),
            ProductDbEntity.FromReader,
            "PRODUCT_NOT_FOUND");

    public Task<AxisResult> ReserveStockAsync(ProductId productId, int quantity)
        => db.ExecuteAsync(
            $"UPDATE {ProductsTable.Table} SET {ProductsColumns.Stock} = {ProductsColumns.Stock} - @quantity WHERE {ProductsColumns.ProductId} = @productId",
            b => b.Add("quantity", quantity).Add("productId", productId.ToString()));

    public Task<AxisResult<IProductEntityProperties>> CreateAsync(IProductEntityProperties properties)
        => db.ExecuteAsync(
                $"INSERT INTO {ProductsTable.Table} ({ProductsColumns.All}) VALUES (@productId, @sku, @name, @stock)",
                b => b.Add("productId", properties.ProductId.ToString())
                    .Add("sku", properties.Sku.ToString())
                    .Add("name", properties.Name)
                    .Add("stock", properties.Stock),
                duplicateKeyCode: "PRODUCT_SKU_ALREADY_EXISTS")
            .WithValueAsync(properties);
}
