namespace OboxSteam.Application.Commons;

public static class DifficultyLevelMapper
{
    public static bool TryMapFromCsv(string raw, out int level)
    {
        level = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "easy":
                level = 1;
                return true;
            case "medium":
                level = 3;
                return true;
            case "hard":
                level = 5;
                return true;
            default:
                return false;
        }
    }
}
