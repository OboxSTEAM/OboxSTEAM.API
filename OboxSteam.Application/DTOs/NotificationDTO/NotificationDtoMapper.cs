using System.Text.Json;
using System.Text.Json.Serialization;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.DTOs.NotificationDTO;

/// <summary>
/// Maps persisted notification rows to API / SignalR DTOs, including typed deeplink payload.
/// </summary>
public static class NotificationDtoMapper
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static NotificationDto ToDto(Notification entity) => new()
    {
        Id = entity.Id,
        RecipientUserId = entity.RecipientUserId,
        Type = entity.Type,
        Title = entity.Title,
        Body = entity.Body,
        Payload = DeserializePayload(entity.PayloadJson),
        PayloadJson = entity.PayloadJson,
        ReadAt = entity.ReadAt,
        ActorUserId = entity.ActorUserId,
        EntityType = entity.EntityType,
        EntityId = entity.EntityId,
        CreatedAt = entity.CreatedAt
    };

    public static NotificationPayload? DeserializePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NotificationPayload>(payloadJson, PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? SerializePayload(NotificationPayload? payload)
    {
        if (payload is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(payload, PayloadJsonOptions);
    }
}
