using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Text.RegularExpressions;

namespace OboxSteam.API.Architecture;

/// <summary>
/// Masks personal data in log records before the OTLP exporter ships them to Traceway.
/// Must be registered before the exporter. Console logging is not affected.
/// </summary>
public sealed partial class TelemetryLogRedactionProcessor : BaseProcessor<LogRecord>
{
    private const string REDACTED = "[redacted]";

    /// <summary>Structured-log keys whose whole value is free text that may describe a student or carry a raw payload.</summary>
    private static readonly HashSet<string> RedactedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Desc",
        "Body",
        "Raw",
    };

    public override void OnEnd(LogRecord data)
    {
        var replacements = new List<(string Original, string Masked)>();

        if (data.Attributes is { Count: > 0 } attributes)
        {
            var redacted = new List<KeyValuePair<string, object?>>(attributes.Count);
            foreach (var attribute in attributes)
            {
                if (attribute.Value is not string text || attribute.Key == "{OriginalFormat}")
                {
                    redacted.Add(attribute);
                    continue;
                }

                var masked = RedactedKeys.Contains(attribute.Key) ? REDACTED : MaskEmails(text);
                if (!string.Equals(masked, text, StringComparison.Ordinal) && text.Length > 0)
                {
                    replacements.Add((text, masked));
                }

                redacted.Add(new KeyValuePair<string, object?>(attribute.Key, masked));
            }

            data.Attributes = redacted;
        }

        if (data.FormattedMessage is { } message)
        {
            foreach (var (original, masked) in replacements)
            {
                message = message.Replace(original, masked, StringComparison.Ordinal);
            }

            data.FormattedMessage = MaskEmails(message);
        }

        if (data.Body is { } body)
        {
            data.Body = MaskEmails(body);
        }
    }

    private static string MaskEmails(string text) =>
        EmailPattern().Replace(text, match => $"{match.Groups["first"].Value}***@{match.Groups["domain"].Value}");

    [GeneratedRegex(@"(?<first>[A-Za-z0-9])[A-Za-z0-9._%+-]*@(?<domain>[A-Za-z0-9.-]+\.[A-Za-z]{2,})")]
    private static partial Regex EmailPattern();
}
