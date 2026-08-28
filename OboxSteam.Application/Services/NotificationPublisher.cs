using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationRecipientResolver _recipientResolver;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<NotificationPublisher> _logger;

    public NotificationPublisher(
        IUnitOfWork unitOfWork,
        INotificationRecipientResolver recipientResolver,
        INotificationDispatcher dispatcher,
        ILogger<NotificationPublisher> logger)
    {
        _unitOfWork = unitOfWork;
        _recipientResolver = recipientResolver;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await PublishManyAsync(new[] { command }, cancellationToken);
    }

    public async Task PublishManyAsync(
        IReadOnlyList<NotificationCommand> commands,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (commands.Count == 0)
        {
            return;
        }

        var entities = new List<Notification>();

        foreach (var command in commands)
        {
            var recipients = await _recipientResolver.ResolveAsync(command.Audience, cancellationToken);
            if (recipients.Count == 0)
            {
                _logger.LogDebug(
                    "Notification {Type} resolved to zero recipients; skipping.",
                    command.Type);
                continue;
            }

            var distinctRecipients = Deduplicate(recipients);
            var displayNames = await LoadDisplayNamesAsync(command, distinctRecipients);

            foreach (var recipient in distinctRecipients)
            {
                var tokens = MergeTokens(command, recipient, displayNames);
                var copy = NotificationTemplateRenderer.Interpolate(
                    command.Templates.Resolve(recipient.Role),
                    tokens);

                var payload = ClonePayloadForRecipient(
                    command.Payload,
                    recipient.ContextStudentId,
                    command.ActorUserId,
                    displayNames);
                var payloadJson = NotificationDtoMapper.SerializePayload(payload);

                entities.Add(new Notification
                {
                    RecipientUserId = recipient.UserId,
                    Type = command.Type,
                    Title = copy.Title,
                    Body = copy.Body,
                    PayloadJson = payloadJson,
                    ActorUserId = command.ActorUserId,
                    EntityType = command.EntityType,
                    EntityId = command.EntityId
                });
            }
        }

        if (entities.Count == 0)
        {
            return;
        }

        await _unitOfWork.Notifications.AddRangeAsync(entities);
        await _unitOfWork.SaveChangesAsync();

        var dtos = entities.Select(MapToDto).ToList();

        try
        {
            await _dispatcher.DispatchManyAsync(dtos, cancellationToken);
        }
        catch (Exception ex)
        {
            // Persist succeeded; log push failures so inbox remains the source of truth.
            _logger.LogWarning(ex, "Failed to dispatch {Count} notifications over SignalR.", dtos.Count);
        }
    }

    private static IReadOnlyList<NotificationRecipient> Deduplicate(
        IReadOnlyList<NotificationRecipient> recipients)
        => recipients
            .GroupBy(r => (r.UserId, r.ContextStudentId))
            .Select(g => g.First())
            .ToList();

    private async Task<IReadOnlyDictionary<Guid, string>> LoadDisplayNamesAsync(
        NotificationCommand command,
        IReadOnlyList<NotificationRecipient> recipients)
    {
        var ids = new HashSet<Guid>();
        foreach (var recipient in recipients)
        {
            if (recipient.ContextStudentId is not null && recipient.ContextStudentId.Value != Guid.Empty)
            {
                ids.Add(recipient.ContextStudentId.Value);
            }
        }

        if (command.ActorUserId is not null && command.ActorUserId.Value != Guid.Empty)
        {
            ids.Add(command.ActorUserId.Value);
        }

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var users = await _unitOfWork.Users.GetAllAsync(u => ids.Contains(u.Id));
        return users.ToDictionary(u => u.Id, DisplayName);
    }

    private static string DisplayName(User user)
        => !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName! : user.Email;

    private static Dictionary<string, string> MergeTokens(
        NotificationCommand command,
        NotificationRecipient recipient,
        IReadOnlyDictionary<Guid, string> displayNames)
    {
        var tokens = new Dictionary<string, string>(command.Tokens, StringComparer.Ordinal);

        if (recipient.ContextStudentId is not null
            && displayNames.TryGetValue(recipient.ContextStudentId.Value, out var studentName)
            && !string.IsNullOrWhiteSpace(studentName))
        {
            tokens[NotificationTokenKeys.StudentName] = studentName;
        }

        if (command.ActorUserId is not null
            && displayNames.TryGetValue(command.ActorUserId.Value, out var actorName)
            && !string.IsNullOrWhiteSpace(actorName))
        {
            tokens[NotificationTokenKeys.ActorName] = actorName;
        }

        return tokens;
    }

    private static NotificationPayload? ClonePayloadForRecipient(
        NotificationPayload? payload,
        Guid? contextStudentId,
        Guid? actorUserId,
        IReadOnlyDictionary<Guid, string> displayNames)
    {
        if (payload is null && contextStudentId is null)
        {
            return null;
        }

        var clone = payload?.Clone() ?? new NotificationPayload();
        if (contextStudentId is not null && contextStudentId.Value != Guid.Empty)
        {
            clone.StudentId = contextStudentId.Value;
            if (displayNames.TryGetValue(contextStudentId.Value, out var studentName)
                && !string.IsNullOrWhiteSpace(studentName))
            {
                clone.StudentName = studentName;
            }
        }

        if (actorUserId is not null
            && actorUserId.Value != Guid.Empty
            && displayNames.TryGetValue(actorUserId.Value, out var actorName)
            && !string.IsNullOrWhiteSpace(actorName))
        {
            clone.ActorName = actorName;
        }

        return clone;
    }

    private static NotificationDto MapToDto(Notification entity) => NotificationDtoMapper.ToDto(entity);
}
