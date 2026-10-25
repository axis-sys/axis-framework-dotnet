using Scaffolds.ECommerce.Contracts.Driving.Auth.v1;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using Scaffolds.ECommerce.SharedKernel.Auth;

namespace Scaffolds.ECommerce.Host.Controllers.Auth;

/// <summary>
/// Sessions of this API: the token exchange that opens one. It is the only endpoint that accepts a token of an
/// external identity provider (Microsoft Entra or Google Accounts); every other endpoint takes the session
/// access token it issues.
/// </summary>
[ApiController]
[Tags("Auth")]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sessions")]
public sealed class SessionsV1Controller(IAuthFacade authFacade) : ControllerBase
{
    /// <summary>Exchanges a valid external-provider token for a session access token of this API.</summary>
    /// <remarks>
    /// Send the Microsoft Entra or the Google Accounts token as the bearer credential. The customer is
    /// provisioned on first sight; the access token returned authenticates every other endpoint.
    /// </remarks>
    /// <response code="200">Session access token issued.</response>
    /// <response code="400">The external identity carries an email or a name that violates a validation rule.</response>
    /// <response code="401">Missing or invalid external token, or a token without a usable subject.</response>
    [HttpPost]
    [Authorize(AuthenticationSchemes = AuthSchemes.ExternalIdentities)]
    [ProducesResponseType<RequestTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> RequestTokenAsync()
        => HttpContext.SendAsync(authFacade.RequestTokenAsync(new RequestTokenCommand()));
}
