using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

/// <summary>Development-only endpoints for notification smoke testing.</summary>
[Route("api/notifications/test")]
[ApiController]
[Authorize(Roles = "SuperAdmin,Manager")]
public sealed class NotificationTestController : ControllerBase
{
    private readonly INotificationSmokeTestService _smokeTestService;
    private readonly IHostEnvironment _environment;

    public NotificationTestController(
        INotificationSmokeTestService smokeTestService,
        IHostEnvironment environment)
    {
        _smokeTestService = smokeTestService;
        _environment = environment;
    }

    [HttpPost("publish-all-types")]
    [SwaggerOperation(
        Summary = "Smoke-test publish all NotificationTypes",
        Description = "Development only. Publishes every NotificationCatalog type using seeded users/classes "
            + "(student1–5, superadmin, manager, parent, mentor, CLS-OPEN-001).")]
    [ProducesResponseType(typeof(ApiResult<NotificationSmokeTestResultDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> PublishAllTypes(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return BadRequest(ApiResult<object>.Failure("400", "Notification smoke test is only available in Development."));
        }

        var result = await _smokeTestService.PublishAllCatalogTypesAsync(cancellationToken);
        return Ok(ApiResult<NotificationSmokeTestResultDto>.Success(
            result,
            "200",
            "Notification smoke test completed."));
    }
}
