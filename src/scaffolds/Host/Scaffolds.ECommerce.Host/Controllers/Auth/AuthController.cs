using System.Net;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestCode;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Validate;

namespace Scaffolds.ECommerce.Host.Controllers.Auth;

/// <summary>
/// Two-phase sign-in, phase one: exchanges a valid external-provider token (Microsoft Entra or
/// Google Accounts) for this API's own bootstrap access token, plus the email-validation round trip.
/// </summary>
[ApiController]
[Tags("Auth")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(
    IAuthFacade authFacade,
    IEmailValidationFacade emailValidationFacade) : ControllerBase
{
    /// <summary>Exchanges a valid Microsoft Entra token for a bootstrap access token.</summary>
    /// <response code="200">Bootstrap token issued.</response>
    /// <response code="401">The Microsoft Entra token carries no usable subject.</response>
    [HttpPost("ms-entra")]
    [Authorize(AuthenticationSchemes = AuthSchemes.MsEntra)]
    [ProducesResponseType<RequestTokenResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> MsEntraLoginAsync()
        => HttpContext.SendAsync(authFacade.RequestTokenAsync(new RequestTokenCommand()));

    /// <summary>Exchanges a valid Google Accounts token for a bootstrap access token.</summary>
    /// <response code="200">Bootstrap token issued.</response>
    /// <response code="401">The Google token carries no usable subject.</response>
    [HttpPost("google")]
    [Authorize(AuthenticationSchemes = AuthSchemes.Google)]
    [ProducesResponseType<RequestTokenResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GoogleLoginAsync()
        => HttpContext.SendAsync(authFacade.RequestTokenAsync(new RequestTokenCommand()));

    /// <summary>Emails a one-time validation code to the authenticated customer.</summary>
    /// <response code="200">Code sent to the email on record.</response>
    /// <response code="401">Missing or invalid bootstrap token.</response>
    [HttpPost("email-validation/request")]
    [Authorize]
    [ProducesResponseType<RequestEmailValidationResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> RequestEmailValidationAsync()
        => HttpContext.SendAsync(emailValidationFacade.RequestCodeAsync(new RequestEmailValidationCommand()));

    /// <summary>
    /// Proves ownership of the email on record by echoing back the received code. Accepts the run and
    /// answers 202: the mark (Customers BC) and the code removal (Auth BC) commit in distinct moments,
    /// so the cross-BC work runs as a saga — poll the status route until a terminal status.
    /// </summary>
    /// <param name="command">The one-time code received by email.</param>
    /// <response code="202">Validation run accepted; poll the status route.</response>
    /// <response code="400">The code does not match the one sent by email.</response>
    /// <response code="401">Missing or invalid bootstrap token.</response>
    [HttpPost("email-validation/validate")]
    [Authorize]
    [ProducesResponseType<ValidateEmailResponse>(StatusCodes.Status202Accepted)]
    public Task<IActionResult> ValidateEmailAsync([FromBody] ValidateEmailCommand command)
        => HttpContext.SendAsync(emailValidationFacade.ValidateAsync(command), HttpStatusCode.Accepted);

    /// <summary>Reads the status of an email-validation run.</summary>
    /// <param name="sagaId">Saga id returned when the validation was accepted.</param>
    /// <response code="200">The validation run.</response>
    /// <response code="401">Missing or invalid bootstrap token.</response>
    /// <response code="404">No validation run with that id.</response>
    [HttpGet("email-validation/validate/{sagaId}")]
    [Authorize]
    [ProducesResponseType<GetValidationStatusResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetValidationStatusAsync(string sagaId)
        => HttpContext.SendAsync(emailValidationFacade.GetValidationStatusAsync(new GetValidationStatusQuery { SagaId = sagaId }));
}
