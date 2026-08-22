using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<Pagination<NotificationDto>> GetMyNotificationsAsync(
        int page,
        int pageSize,
        bool? unreadOnly)
    {
        var userId = RequireCurrentUserId();
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 10;
        }

        if (pageSize > 50)
        {
            pageSize = 50;
        }

        var query = _unitOfWork.Notifications.GetQueryable()
            .Where(n => n.RecipientUserId == userId);

        if (unreadOnly == true)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        query = query.OrderByDescending(n => n.CreatedAt);

        var totalCount = query.Count();
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = items.Select(NotificationDtoMapper.ToDto).ToList();

        return new Pagination<NotificationDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<NotificationUnreadCountDto> GetUnreadCountAsync()
    {
        var userId = RequireCurrentUserId();
        var count = await _unitOfWork.Notifications.GetAllAsync(
            n => n.RecipientUserId == userId && n.ReadAt == null);

        return new NotificationUnreadCountDto { Count = count.Count };
    }

    public async Task MarkReadAsync(Guid notificationId)
    {
        var userId = RequireCurrentUserId();
        var notification = await _unitOfWork.Notifications.FirstOrDefaultAsync(
            n => n.Id == notificationId && n.RecipientUserId == userId);

        if (notification is null)
        {
            throw ErrorHelper.NotFound("Notification not found.");
        }

        if (notification.ReadAt is not null)
        {
            return;
        }

        notification.ReadAt = DateTime.UtcNow;
        await _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogDebug("Marked notification {NotificationId} read for user {UserId}.", notificationId, userId);
    }

    public async Task MarkAllReadAsync()
    {
        var userId = RequireCurrentUserId();
        var unread = await _unitOfWork.Notifications.GetAllAsync(
            n => n.RecipientUserId == userId && n.ReadAt == null);

        if (unread.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var item in unread)
        {
            item.ReadAt = now;
        }

        await _unitOfWork.Notifications.UpdateRange(unread);
        await _unitOfWork.SaveChangesAsync();
    }

    private Guid RequireCurrentUserId()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("User is not authenticated.");
        }

        return userId;
    }
}
