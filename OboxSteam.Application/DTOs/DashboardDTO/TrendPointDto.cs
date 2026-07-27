namespace OboxSteam.Application.DTOs.DashboardDTO;

public class TrendPointDto
{
    /// <summary>Human-readable bucket label (e.g. "2026-07-20", "W30 2026", "Jul 2026").</summary>
    public string Label { get; set; } = null!;

    /// <summary>UTC start of the bucket, ISO-8601 when serialized.</summary>
    public DateTime BucketStart { get; set; }

    public decimal Value { get; set; }
}
