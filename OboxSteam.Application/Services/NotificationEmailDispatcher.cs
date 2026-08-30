using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class NotificationEmailDispatcher : INotificationEmailDispatcher
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationEmailDispatcher> _logger;

    public NotificationEmailDispatcher(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<NotificationEmailDispatcher> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task DispatchManyAsync(
        IReadOnlyList<NotificationDto> notifications,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        if (notifications.Count == 0)
        {
            return;
        }

        var eligible = notifications
            .Where(n => NotificationEmailPriority.ShouldEmail(n.Type))
            .ToList();
        if (eligible.Count == 0)
        {
            return;
        }

        var recipientIds = eligible
            .Select(n => n.RecipientUserId)
            .Distinct()
            .ToList();

        var users = await _unitOfWork.Users.GetAllAsync(u => recipientIds.Contains(u.Id));
        var emailsByUserId = users
            .Where(u => u.Status == AccountStatus.Active && !string.IsNullOrWhiteSpace(u.Email))
            .ToDictionary(u => u.Id, u => u.Email);

        foreach (var notification in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!emailsByUserId.TryGetValue(notification.RecipientUserId, out var to))
            {
                continue;
            }

            try
            {
                await _emailService.SendInboxNotificationEmailAsync(new InboxNotificationEmailDto
                {
                    To = to,
                    Title = notification.Title,
                    Body = notification.Body
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to email notification {NotificationId} ({Type}) to {RecipientUserId}.",
                    notification.Id,
                    notification.Type,
                    notification.RecipientUserId);
            }
        }
    }
}
