using OboxSteam.Application.Commons;

namespace OboxSteam.Application.DTOs.DashboardDTO;

public class RevenueOverviewDto
{
    public decimal TotalRevenue { get; set; }

    public decimal RevenueInRange { get; set; }

    /// <summary>Sum of successful payments in the previous adjacent window of equal length.</summary>
    public decimal RevenueInPreviousRange { get; set; }

    public decimal AverageOrderValue { get; set; }

    public int PendingPaymentRequestsCount { get; set; }

    public decimal PendingPaymentRequestsAmount { get; set; }

    public decimal RefundedAmount { get; set; }

    public int InvoiceCount { get; set; }

    public TrendSeriesDto RevenueTrend { get; set; } = null!;

    public List<RevenueByGatewayDto> RevenueByGateway { get; set; } = new();

    public Pagination<TopProgramRevenueDto> TopProgramsByRevenue { get; set; } = null!;
}
