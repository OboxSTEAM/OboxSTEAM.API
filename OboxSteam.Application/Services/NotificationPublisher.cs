using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class NotificationPublisher : INotificationPublisher
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
            var recipientIds = await _recipientResolver.ResolveAsync(command.Audience, cancellationToken);
            if (recipientIds.Count == 0)
            {
                _logger.LogDebug(
                    "Notification {Type} resolved to zero recipients; skipping.",
                    command.Type);
                continue;
            }

            var payloadJson = command.Payload is null
                ? null
                : JsonSerializer.Serialize(command.Payload, PayloadJsonOptions);

            foreach (var recipientId in recipientIds.Distinct())
            {
                entities.Add(new Notification
                {
                    RecipientUserId = recipientId,
                    Type = command.Type,
                    Title = command.Title,
                    Body = command.Body,
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

    private static NotificationDto MapToDto(Notification entity) => new()
    {
        Id = entity.Id,
        RecipientUserId = entity.RecipientUserId,
        Type = entity.Type,
        Title = entity.Title,
        Body = entity.Body,
        PayloadJson = entity.PayloadJson,
        ReadAt = entity.ReadAt,
        ActorUserId = entity.ActorUserId,
        EntityType = entity.EntityType,
        EntityId = entity.EntityId,
        CreatedAt = entity.CreatedAt
    };
}
