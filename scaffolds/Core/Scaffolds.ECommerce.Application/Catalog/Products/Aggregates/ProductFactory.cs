using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;
using Scaffolds.ECommerce.Domain.Catalog.Products;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Application.Catalog.Products.Aggregates;

internal interface IProductFactory
{
    sealed record NewArgs
    {
        public required Sku Sku { get; init; }
        public required string Name { get; init; }
        public required int InitialStock { get; init; }
    }

    Task<AxisResult<IProductEntityProperties>> CreateAsync(NewArgs args);
}

internal sealed class ProductFactory(IProductsWritePort products) : IProductFactory
{
    public Task<AxisResult<IProductEntityProperties>> CreateAsync(IProductFactory.NewArgs args)
        => products.CreateAsync(new ProductProperties(ProductId.New, args.Sku, args.Name, args.InitialStock));
}
