using System.Net;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.GetOrder;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;

namespace Scaffolds.ECommerce.Host.Controllers.Catalog;

/// <summary>
/// The orders of the catalog: an order is submitted for a cart whose product the authenticated customer
/// checked out, and read back priced and described. Synchronous; needs the session access token.
/// </summary>
[ApiController]
[Tags("Catalog")]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
public sealed class OrdersV1Controller(IOrdersFacade ordersFacade) : ControllerBase
{
    /// <summary>Submits an order for the product checked out into a cart.</summary>
    /// <remarks>
    /// The quantity and the cart are validated together, so a single call reports every violated rule. A
    /// cart whose checkout has not been associated yet answers 404; retry once the association lands.
    /// </remarks>
    /// <param name="command">Quantity and cart of the order.</param>
    /// <response code="201">Order submitted.</response>
    /// <response code="400">The quantity or the cart id violates a validation rule.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No checked-out product is associated with the cart.</response>
    [HttpPost]
    [ProducesResponseType<SubmitOrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> SubmitOrderAsync([FromBody] SubmitOrderCommand command)
        => HttpContext.SendAsync(ordersFacade.SubmitOrderAsync(command), HttpStatusCode.Created);

    /// <summary>Reads an order of the authenticated customer, priced and described.</summary>
    /// <remarks>
    /// The order is priced with the catalog's unit price and discount rate. An order of another customer
    /// answers 404, like an unknown one. A unit price that is not positive leaves the order readable, with
    /// the codes of why it has no total in its summary and description.
    /// </remarks>
    /// <param name="orderId">Id of the order to read.</param>
    /// <response code="200">The order, priced and described.</response>
    /// <response code="400">The order id is not a valid id.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No order with that id among the orders of the authenticated customer.</response>
    [HttpGet("{orderId}")]
    [ProducesResponseType<GetOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetOrderAsync(string orderId)
        => HttpContext.SendAsync(ordersFacade.GetOrderAsync(new GetOrderQuery { OrderId = orderId }));
}
