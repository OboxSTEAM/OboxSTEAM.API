using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons;

/// <summary>Allowed chat attachment types, keyed by lower-case extension.</summary>
public static class DiscussionAttachmentFileRules
{
    public const long MaxSizeBytes = 20L * 1024 * 1024;

    private const string OctetStream = "application/octet-stream";

    private static readonly Dictionary<string, (DiscussionAttachmentKind Kind, string[] ContentTypes)> Types =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = (DiscussionAttachmentKind.Image, ["image/png"]),
            [".jpg"] = (DiscussionAttachmentKind.Image, ["image/jpeg", "image/pjpeg"]),
            [".jpeg"] = (DiscussionAttachmentKind.Image, ["image/jpeg", "image/pjpeg"]),
            [".gif"] = (DiscussionAttachmentKind.Image, ["image/gif"]),
            [".webp"] = (DiscussionAttachmentKind.Image, ["image/webp"]),
            [".pdf"] = (DiscussionAttachmentKind.File, ["application/pdf"]),
            [".doc"] = (DiscussionAttachmentKind.File, ["application/msword"]),
            [".docx"] = (DiscussionAttachmentKind.File, ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"]),
            [".ppt"] = (DiscussionAttachmentKind.File, ["application/vnd.ms-powerpoint"]),
            [".pptx"] = (DiscussionAttachmentKind.File, ["application/vnd.openxmlformats-officedocument.presentationml.presentation"]),
            [".xls"] = (DiscussionAttachmentKind.File, ["application/vnd.ms-excel"]),
            [".xlsx"] = (DiscussionAttachmentKind.File, ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]),
            [".zip"] = (DiscussionAttachmentKind.File, ["application/zip", "application/x-zip-compressed", "application/x-zip"]),
        };

    /// <summary>
    /// Resolves kind and canonical content type. The declared content type must match the extension;
    /// a missing or generic <c>application/octet-stream</c> type is accepted.
    /// </summary>
    public static bool TryResolve(
        string fileName,
        string? declaredContentType,
        out DiscussionAttachmentKind kind,
        out string contentType)
    {
        kind = default;
        contentType = string.Empty;
        if (!Types.TryGetValue(Path.GetExtension(fileName), out var rule))
        {
            return false;
        }

        var declared = declaredContentType?.Split(';')[0].Trim();
        if (!string.IsNullOrEmpty(declared)
            && !declared.Equals(OctetStream, StringComparison.OrdinalIgnoreCase)
            && !rule.ContentTypes.Contains(declared, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        kind = rule.Kind;
        contentType = rule.ContentTypes[0];
        return true;
    }
}
