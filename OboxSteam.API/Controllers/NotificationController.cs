using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/notifications")]
[ApiController]
[Authorize]
public sealed class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "List my notifications",
        Description = "Returns a paginated inbox for the current user. Optionally filter to unread only.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<NotificationDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? unreadOnly = null)
    {
        var result = await _notificationService.GetMyNotificationsAsync(page, pageSize, unreadOnly);
        return Ok(ApiResult<Pagination<NotificationDto>>.Success(result, "200", "Notifications retrieved successfully."));
    }

    [HttpGet("unread-count")]
    [SwaggerOperation(
        Summary = "Get unread notification count",
        Description = "Returns the number of unread notifications for the current user.")]
    [ProducesResponseType(typeof(ApiResult<NotificationUnreadCountDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var result = await _notificationService.GetUnreadCountAsync();
        return Ok(ApiResult<NotificationUnreadCountDto>.Success(result, "200", "Unread count retrieved successfully."));
    }

    [HttpPatch("{id:guid}/read")]
    [SwaggerOperation(
        Summary = "Mark notification as read",
        Description = "Marks a single notification as read. Only the recipient may do this.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> MarkRead([FromRoute] Guid id)
    {
        await _notificationService.MarkReadAsync(id);
        return Ok(ApiResult<object>.Success(null!, "200", "Notification marked as read."));
    }

    [HttpPatch("read-all")]
    [SwaggerOperation(
        Summary = "Mark all notifications as read",
        Description = "Marks all unread notifications for the current user as read.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notificationService.MarkAllReadAsync();
        return Ok(ApiResult<object>.Success(null!, "200", "All notifications marked as read."));
    }
}
