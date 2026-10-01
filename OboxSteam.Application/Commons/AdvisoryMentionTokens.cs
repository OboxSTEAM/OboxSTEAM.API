using System.Text.RegularExpressions;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Commons;

/// <summary>Parses <c>@[Type:uuid]</c> mention tokens in discussion text.</summary>
public static class AdvisoryMentionTokens
{
    private static readonly Regex TokenPattern = new(
        @"@\[(?<type>[A-Za-z]+):(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\]",
        RegexOptions.Compiled);

    /// <summary>
    /// Distinct mentions in order of first occurrence. Tokens with an unknown type stay plain text.
    /// </summary>
    public static IReadOnlyList<(ProgramAdvisoryTargetType TargetType, Guid TargetId)> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var result = new List<(ProgramAdvisoryTargetType, Guid)>();
        foreach (Match match in TokenPattern.Matches(text))
        {
            if (!TryParseType(match.Groups["type"].Value, out var targetType))
            {
                continue;
            }

            var mention = (targetType, Guid.Parse(match.Groups["id"].Value));
            if (!result.Contains(mention))
            {
                result.Add(mention);
            }
        }

        return result;
    }

    public static string Format(ProgramAdvisoryTargetType targetType, Guid targetId)
        => $"@[{targetType}:{targetId:D}]";

    private static bool TryParseType(string value, out ProgramAdvisoryTargetType targetType)
        => Enum.TryParse(value, ignoreCase: true, out targetType)
           && Enum.IsDefined(targetType);
}
