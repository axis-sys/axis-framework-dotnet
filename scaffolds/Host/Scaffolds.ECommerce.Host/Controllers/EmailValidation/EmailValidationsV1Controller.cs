using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestEmailValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;
using Scaffolds.ECommerce.Host.Authorization;
using Scaffolds.ECommerce.SharedKernel.Authorization;

namespace Scaffolds.ECommerce.Host.Controllers.EmailValidation;

/// <summary>
/// Validation of the authenticated customer's email: a one-time code is emailed, then echoed back. The echo
/// starts a saga run (the mark in Customers and the code removal commit apart) answered with 202, polled and
/// cancelled on its run resource by callers holding the permission of the validation runs.
/// </summary>
[ApiController]
[Tags("EmailValidation")]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/email-validations")]
public sealed class EmailValidationsV1Controller(IEmailValidationFacade emailValidationFacade) : ControllerBase
{
    private const string ValidationRunRoute = "EmailValidationsV1.ValidationRun";

    /// <summary>Emails a one-time validation code to the authenticated customer.</summary>
    /// <response code="200">Code sent to the email on record.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">The customer of the access token is not on record.</response>
    [HttpPost("codes")]
    [ProducesResponseType<RequestEmailValidationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> RequestEmailValidationAsync()
        => HttpContext.SendAsync(emailValidationFacade.RequestEmailValidationAsync(new RequestEmailValidationCommand()));

    /// <summary>Proves ownership of the email on record by echoing back the code it received.</summary>
    /// <remarks>
    /// Accepts the run and answers 202 with its saga id, the <c>Location</c> of its run resource and a
    /// <c>Retry-After</c>: a caller holding the permission of the validation runs polls
    /// <c>GET /api/v1/email-validations/{sagaId}</c> until a terminal status.
    /// </remarks>
    /// <param name="command">The one-time code received by email.</param>
    /// <response code="202">Validation run accepted; poll its run resource.</response>
    /// <response code="400">The code is missing or does not match the one sent by email.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No validation code was requested for the customer.</response>
    [HttpPost]
    [ProducesResponseType<ValidateEmailResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> ValidateEmailAsync([FromBody] ValidateEmailCommand command)
        => HttpContext.SendAcceptedAsync(
            emailValidationFacade.ValidateEmailAsync(command),
            accepted => Url.RouteUrl(ValidationRunRoute, new { sagaId = accepted.SagaId }));

    /// <summary>Reads the status of an email-validation run.</summary>
    /// <remarks>
    /// Requires the permission of the validation runs (<c>email-validation.validate-email-runs</c>), held by
    /// members granted it and by administrators. A caller without it gets the same 404 as for a run that does
    /// not exist, so the existence of a run never leaks.
    /// </remarks>
    /// <param name="sagaId">Saga id returned when the validation was accepted.</param>
    /// <response code="200">The validation run.</response>
    /// <response code="400">The saga id is not a valid id.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No validation run with that id visible to the caller.</response>
    [HttpGet("{sagaId}", Name = ValidationRunRoute)]
    [RequirePermission(PermissionCodes.ValidateEmailRuns, ConcealsSagaRun = true)]
    [ProducesResponseType<GetValidationStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetValidationStatusAsync(string sagaId)
        => HttpContext.SendAsync(emailValidationFacade.GetValidationStatusAsync(new GetValidationStatusQuery { SagaId = sagaId }));

    /// <summary>Stops an email-validation run gracefully.</summary>
    /// <remarks>
    /// A run no one has started yet ends <c>Cancelled</c> at once; a running one becomes <c>Cancelling</c>, finishes
    /// the stage in progress and ends <c>Cancelled</c> before the next one. Nothing is compensated: the work already
    /// done stays done. Requires the permission of the validation runs; a caller without it gets the same 404 as for
    /// a run that does not exist.
    /// </remarks>
    /// <param name="sagaId">Saga id returned when the validation was accepted.</param>
    /// <response code="200">Cancel requested: the run is <c>Cancelling</c> or <c>Cancelled</c>.</response>
    /// <response code="400">The saga id is not a valid id.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="404">No validation run with that id visible to the caller.</response>
    /// <response code="409">The run already finished or is compensating, so it cannot be cancelled.</response>
    [HttpPost("{sagaId}/cancel")]
    [RequirePermission(PermissionCodes.ValidateEmailRuns, ConcealsSagaRun = true)]
    [ProducesResponseType<CancelValidationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> CancelValidationAsync(string sagaId)
        => HttpContext.SendAsync(emailValidationFacade.CancelValidationAsync(new CancelValidationCommand { SagaId = sagaId }));
}
