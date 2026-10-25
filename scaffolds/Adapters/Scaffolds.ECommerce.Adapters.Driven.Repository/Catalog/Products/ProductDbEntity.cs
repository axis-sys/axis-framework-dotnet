using System.Data.Common;
using Scaffolds.ECommerce.Domain.Catalog.Products;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Products;

internal sealed record ProductDbEntity(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties
{
    internal static ProductDbEntity FromReader(DbDataReader reader)
        => new(
            (ProductId)reader.GetString(0),
            (Sku)reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3)
        );
}
