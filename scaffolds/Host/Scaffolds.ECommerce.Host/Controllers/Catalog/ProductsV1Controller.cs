using System.Net;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.RegisterProduct;
using Scaffolds.ECommerce.Host.Authorization;
using Scaffolds.ECommerce.SharedKernel.Authorization;

namespace Scaffolds.ECommerce.Host.Controllers.Catalog;

/// <summary>
/// The products of the catalog: registration, reads and checkout. Every operation is synchronous and needs
/// the session access token; registering a product also needs the catalog permission.
/// </summary>
[ApiController]
[Tags("Catalog")]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
public sealed class ProductsV1Controller(IProductsFacade productsFacade) : ControllerBase
{
    /// <summary>Reads a single product by id.</summary>
    /// <param name="productId">Id of the product to read.</param>
    /// <response code="200">The product.</response>
    /// <response code="400">The product id is not a valid id.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No product with that id.</response>
    [HttpGet("{productId}")]
    [ProducesResponseType<GetProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetProductAsync(string productId)
        => HttpContext.SendAsync(productsFacade.GetProductAsync(new GetProductQuery { ProductId = productId }));

    /// <summary>Registers a new product in the catalog.</summary>
    /// <remarks>
    /// Idempotent by SKU: registering a SKU that is already in the catalog answers the product registered
    /// under it. The SKU is case-insensitive and kept in upper case.
    /// </remarks>
    /// <param name="command">SKU, name and initial stock of the product.</param>
    /// <response code="201">Product registered, or the product already registered under that SKU.</response>
    /// <response code="400">The SKU, the name or the initial stock violates a validation rule.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="403">Authenticated but lacking the permission to register products.</response>
    [HttpPost]
    [RequirePermission(PermissionCodes.CatalogRegisterProducts)]
    [ProducesResponseType<RegisterProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> RegisterProductAsync([FromBody] RegisterProductCommand command)
        => HttpContext.SendAsync(productsFacade.RegisterProductAsync(command), HttpStatusCode.Created);

    /// <summary>Reserves stock of the product for a cart of the authenticated customer.</summary>
    /// <remarks>
    /// The cart is associated with the product asynchronously, right after the reservation commits; submit
    /// the order for the cart with <c>POST /api/v1/orders</c>.
    /// </remarks>
    /// <param name="productId">Id of the product being checked out.</param>
    /// <param name="command">Cart and quantity to reserve.</param>
    /// <response code="200">Stock reserved for the cart.</response>
    /// <response code="400">The product id, the cart id or the quantity violates a validation rule.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No product with that id.</response>
    /// <response code="422">The quantity exceeds the stock available.</response>
    [HttpPost("{productId}/checkout")]
    [ProducesResponseType<CheckoutProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CheckoutProductAsync(string productId, [FromBody] CheckoutProductCommand command)
        => HttpContext.SendAsync(productsFacade.CheckoutProductAsync(command with { ProductId = productId }));
}
