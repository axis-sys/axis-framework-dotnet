using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;
using Scaffolds.ECommerce.Host.Authorization;

namespace Scaffolds.ECommerce.Host.Controllers.Authorization;

/// <summary>
/// The members of the Authorization context and the grants they hold, reserved to administrators. A member is a
/// customer granted permission codes; disabling the membership refuses every one of them.
/// </summary>
[ApiController]
[Tags("Authorization")]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/members")]
public sealed class MembersV1Controller(IAuthorizationFacade authorizationFacade) : ControllerBase
{
    /// <summary>Replaces the grants of a member, making the customer a member on its first grant.</summary>
    /// <remarks>
    /// The permission checks cache each caller's grants; the change evicts the member's cached grants as soon as
    /// it is delivered, so the member's next requests are decided on the new grants.
    /// </remarks>
    /// <param name="customerId">Id of the customer whose grants are replaced.</param>
    /// <param name="command">Whether the membership is disabled and the permission codes it holds.</param>
    /// <response code="200">The grants the member now holds.</response>
    /// <response code="400">The customer id, the disabled flag or a permission code violates a validation rule.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="403">Authenticated but not an administrator.</response>
    /// <response code="404">No customer with that id.</response>
    [HttpPut("{customerId}/grants")]
    [ProducesResponseType<UpdateGrantsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateGrantsAsync(string customerId, [FromBody] UpdateGrantsCommand command)
        => HttpContext.SendAsync(authorizationFacade.UpdateGrantsAsync(command with { CustomerId = customerId }));
}
