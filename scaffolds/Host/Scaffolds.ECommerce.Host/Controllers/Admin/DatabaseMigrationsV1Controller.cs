using Scaffolds.ECommerce.Contracts.Driving.Admin.v1;
using Scaffolds.ECommerce.Contracts.Driving.Admin.v1.MigrateDatabase;
using Scaffolds.ECommerce.Host.Authorization;

namespace Scaffolds.ECommerce.Host.Controllers.Admin;

/// <summary>
/// Schema migrations of the business database, reserved to administrators. A deploy, and the first run on an
/// empty database, migrate through the host's <c>migrate</c> command; this endpoint re-runs the same idempotent
/// migration on demand.
/// </summary>
[ApiController]
[Tags("Admin")]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/database-migrations")]
public sealed class DatabaseMigrationsV1Controller(IAdminFacade adminFacade) : ControllerBase
{
    /// <summary>Applies every pending schema migration, bounded context by bounded context. Safe to repeat.</summary>
    /// <response code="200">Every schema is up to date.</response>
    /// <response code="401">Missing or invalid access token.</response>
    /// <response code="403">Authenticated but not an administrator.</response>
    /// <response code="500">A migration failed; the schemas before it stay migrated.</response>
    /// <response code="503">The database connection string is not configured.</response>
    [HttpPost]
    [ProducesResponseType<MigrateDatabaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> MigrateDatabaseAsync()
        => HttpContext.SendAsync(adminFacade.MigrateDatabaseAsync(new MigrateDatabaseCommand()));
}
