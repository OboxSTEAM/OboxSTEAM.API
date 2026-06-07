namespace OboxSteam.Application.Commons;

public static class QuestionTypeConstants
{
    public const string SingleChoice = "SingleChoice";
    public const string MultipleChoice = "MultipleChoice";

    private static readonly Dictionary<string, string> CsvAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["singlechoice"] = SingleChoice,
        ["multichoice"] = MultipleChoice,
        ["truefalse"] = SingleChoice
    };

    public static bool TryNormalizeFromCsv(string raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return CsvAliases.TryGetValue(raw.Trim(), out normalized!);
    }

    public static bool IsTrueFalseCsvType(string raw)
        => !string.IsNullOrWhiteSpace(raw)
           && raw.Trim().Equals("truefalse", StringComparison.OrdinalIgnoreCase);

    public static bool IsValidCanonical(string value)
        => string.Equals(value, SingleChoice, StringComparison.Ordinal)
           || string.Equals(value, MultipleChoice, StringComparison.Ordinal);
}
