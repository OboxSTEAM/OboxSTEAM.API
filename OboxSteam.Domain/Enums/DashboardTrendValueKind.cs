namespace OboxSteam.Domain.Enums;

/// <summary>
/// Semantically describes a trend series Value so clients can pick formatters
/// without hardcoding per metric (Count | Currency | Percent).
/// </summary>
public enum DashboardTrendValueKind
{
    Count,
    Currency,
    Percent
}
