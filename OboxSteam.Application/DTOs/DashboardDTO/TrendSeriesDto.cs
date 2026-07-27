using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.DashboardDTO;

/// <summary>
/// Trend series with the server-resolved window and value semantics.
/// </summary>
public class TrendSeriesDto
{
    /// <summary>Inclusive lower bound ResolveRange used for this series (UTC).</summary>
    public DateTime FromDate { get; set; }

    /// <summary>Inclusive upper bound ResolveRange used for this series (UTC).</summary>
    public DateTime ToDate { get; set; }

    public DashboardTrendGranularity Granularity { get; set; }

    public DashboardTrendValueKind ValueKind { get; set; }

    public List<TrendPointDto> Points { get; set; } = new();
}
