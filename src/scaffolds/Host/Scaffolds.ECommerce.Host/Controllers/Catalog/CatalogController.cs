using System.Net;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1._Shared;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Checkout;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.GetProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.RegisterProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.SubmitOrder;

namespace Scaffolds.ECommerce.Host.Controllers.Catalog;

/// <summary>The catalog surface: product registration, reads, checkout and orders.</summary>
[ApiController]
[Tags("ECommerce")]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/catalog")]
public sealed class CatalogController(ICatalogFacade facade) : ControllerBase
{
    /// <summary>Reads a single product by id.</summary>
    /// <param name="productId">Id of the product to read.</param>
    /// <response code="200">The product.</response>
    /// <response code="404">No product with that id.</response>
    [AllowAnonymous]
    [HttpGet("{productId}")]
    [ProducesResponseType<GetProductResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetProductAsync(string productId)
        => HttpContext.SendAsync(facade.GetProductAsync(new GetProductQuery { ProductId = productId }));

    /// <summary>Registers a new product in the catalog.</summary>
    /// <param name="command">SKU, name and initial stock of the product.</param>
    /// <response code="201">Product registered.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="403">Authenticated but lacking the catalog write permission.</response>
    /// <response code="409">A product with that SKU already exists.</response>
    [Authorize(Policy = CatalogPolicies.Write)]
    [HttpPost("products")]
    [ProducesResponseType<RegisterProductResponse>(StatusCodes.Status201Created)]
    public Task<IActionResult> RegisterProductAsync([FromBody] RegisterProductCommand command)
        => HttpContext.SendAsync(facade.RegisterProductAsync(command), HttpStatusCode.Created);

    /// <summary>Reserves stock and opens an order for the authenticated customer.</summary>
    /// <param name="productId">Id of the product being checked out.</param>
    /// <param name="command">Quantity to reserve.</param>
    /// <response code="200">Order opened.</response>
    /// <response code="401">Missing or invalid access token.</response>
    [HttpPost("{productId}/checkout")]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> CheckoutAsync(string productId, [FromBody] CheckoutCommand command)
        => HttpContext.SendAsync(facade.CheckoutAsync(command with { ProductId = productId }));

    /// <summary>Submits an order, validating quantity and coupon together.</summary>
    /// <param name="command">Quantity and coupon of the order.</param>
    /// <response code="201">Order submitted.</response>
    /// <response code="400">Quantity or coupon violates a validation rule.</response>
    /// <response code="401">Missing or invalid access token.</response>
    [HttpPost("orders/submit")]
    [ProducesResponseType<SubmitOrderResponse>(StatusCodes.Status201Created)]
    public Task<IActionResult> SubmitOrderAsync([FromBody] SubmitOrderCommand command)
        => HttpContext.SendAsync(facade.SubmitOrderAsync(command), HttpStatusCode.Created);
}
