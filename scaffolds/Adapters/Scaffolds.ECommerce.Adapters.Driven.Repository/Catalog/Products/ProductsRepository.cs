using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Domain.Catalog.Products;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Products;

internal sealed class ProductsRepository(IAxisDbRepository db) : IProductsReaderPort, IProductsWritePort
{
    private const string Select = $"SELECT {ProductsColumns.All}";

    public Task<AxisResult<IProductEntityProperties>> GetByIdAsync(ProductId productId)
        => db.GetAsync<IProductEntityProperties>(
            $"{Select} FROM {ProductsTable.Table} WHERE {ProductsColumns.ProductId} = @productId",
            b => b.Add("productId", productId.ToString()),
            ProductDbEntity.FromReader,
            ProductErrors.ProductNotFound.Code);

    public Task<AxisResult<IProductEntityProperties>> GetBySkuAsync(Sku sku)
        => db.GetAsync<IProductEntityProperties>(
            $"{Select} FROM {ProductsTable.Table} WHERE {ProductsColumns.Sku} = @sku",
            b => b.Add("sku", sku.ToString()),
            ProductDbEntity.FromReader,
            ProductErrors.ProductNotFound.Code);

    public Task<AxisResult<IProductEntityProperties>> CreateAsync(IProductEntityProperties product)
        => db.ExecuteAsync(
                $"INSERT INTO {ProductsTable.Table} ({ProductsColumns.All}) VALUES (@productId, @sku, @name, @stock)",
                b => b.Add("productId", product.ProductId.ToString())
                    .Add("sku", product.Sku.ToString())
                    .Add("name", product.Name)
                    .Add("stock", product.Stock),
                duplicateKeyCode: ProductErrors.SkuAlreadyExists.Code)
            .WithValueAsync(product);

    public Task<AxisResult<IProductEntityProperties>> GetByIdForUpdateAsync(ProductId productId)
        => db.GetAsync<IProductEntityProperties>(
            $"{Select} FROM {ProductsTable.Table} WHERE {ProductsColumns.ProductId} = @productId FOR UPDATE",
            b => b.Add("productId", productId.ToString()),
            ProductDbEntity.FromReader,
            ProductErrors.ProductNotFound.Code);

    public Task<AxisResult> ReserveStockAsync(ProductId productId, int quantity)
        => db.ExecuteAsync(
            $"UPDATE {ProductsTable.Table} SET {ProductsColumns.Stock} = {ProductsColumns.Stock} - @quantity WHERE {ProductsColumns.ProductId} = @productId",
            b => b.Add("quantity", quantity).Add("productId", productId.ToString()));
}
