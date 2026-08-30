using System.ComponentModel;
using System.Reflection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Notifications;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OboxSteam.API.Attributes;

/// <summary>
/// Documents notification deeplink payload shape for OpenAPI consumers (FE codegen).
/// </summary>
public sealed class NotificationPayloadSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(NotificationPayload))
        {
            schema.Title = "NotificationPayload";
            schema.Description =
                "Typed deeplink bag for notification routing. Null/unused keys are omitted at write time. "
                + "Prefer NotificationDto.payload over payloadJson.";
            schema.AdditionalPropertiesAllowed = false;
            ApplyPropertyDescriptions(schema, typeof(NotificationPayload));
            schema.Example = BuildExample();
            return;
        }

        if (context.Type != typeof(NotificationDto))
        {
            return;
        }

        if (schema.Properties.TryGetValue("payload", out var payloadSchema))
        {
            payloadSchema.Description =
                "Typed deep-link ids for client routing. Prefer this over payloadJson.";
            payloadSchema.Nullable = true;
        }

        if (schema.Properties.TryGetValue("payloadJson", out var payloadJsonSchema))
        {
            payloadJsonSchema.Description =
                "Legacy camelCase JSON string of NotificationPayload (same content as payload). Prefer payload.";
            payloadJsonSchema.Nullable = true;
            payloadJsonSchema.Example = new OpenApiString(
                "{\"programId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\","
                + "\"enrollmentId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\","
                + "\"nextActivityId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\","
                + "\"studentId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"}");
        }
    }

    private static void ApplyPropertyDescriptions(OpenApiSchema schema, Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (string.IsNullOrWhiteSpace(description) || property.Name.Length == 0)
            {
                continue;
            }

            var camelName = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            if (schema.Properties.TryGetValue(camelName, out var propertySchema)
                || schema.Properties.TryGetValue(property.Name, out propertySchema))
            {
                propertySchema.Description = description;
            }
        }
    }

    private static OpenApiObject BuildExample() => new()
    {
        ["programId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["enrollmentId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["programEnrollmentId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["studentId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["moduleId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["courseId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["activityId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["nextActivityId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["assignmentId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["classId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["classSessionId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["mediaAssetId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["studentName"] = new OpenApiString("An Nguyen"),
        ["actorName"] = new OpenApiString("Mentor Name"),
        ["className"] = new OpenApiString("Cohort A"),
        ["programName"] = new OpenApiString("Robotics 1")
    };
}
