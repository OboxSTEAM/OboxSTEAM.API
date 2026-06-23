using System.Text.Json;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons;

public static class ActivityResumeStateHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(ActivityResumeStateDto state)
    {
        return JsonSerializer.Serialize(state, JsonOptions);
    }

    public static ActivityResumeStateDto? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ActivityResumeStateDto>(json, JsonOptions);
    }

    public static void ValidateResumeState(ActivityResumeStateDto state)
    {
        if (string.IsNullOrWhiteSpace(state.Kind))
        {
            throw ErrorHelper.BadRequest("resumeState.kind is required.");
        }

        switch (state.Kind.ToLowerInvariant())
        {
            case "video":
                if (!state.PositionSeconds.HasValue || state.PositionSeconds.Value < 0)
                {
                    throw ErrorHelper.BadRequest("resumeState.positionSeconds is required for video checkpoints.");
                }

                break;

            case "pdf":
                if (!state.Page.HasValue || state.Page.Value < 1)
                {
                    throw ErrorHelper.BadRequest("resumeState.page is required for pdf checkpoints (1-based).");
                }

                break;

            case "doc":
                if (!state.ScrollRatio.HasValue || state.ScrollRatio.Value is < 0 or > 1)
                {
                    throw ErrorHelper.BadRequest("resumeState.scrollRatio must be between 0 and 1 for doc checkpoints.");
                }

                break;

            default:
                throw ErrorHelper.BadRequest(
                    "resumeState.kind must be one of: video, pdf, doc.");
        }
    }

    public static CompletionSource? ParseCompletionSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        return source.ToLowerInvariant() switch
        {
            "manual" => CompletionSource.Manual,
            "video" => CompletionSource.Video,
            "reading" => CompletionSource.Reading,
            _ => throw ErrorHelper.BadRequest(
                "source must be one of: manual, video, reading."),
        };
    }

    public static string? ToApiString(CompletionSource? source)
    {
        return source switch
        {
            CompletionSource.Manual => "manual",
            CompletionSource.Video => "video",
            CompletionSource.Reading => "reading",
            _ => null,
        };
    }
}
