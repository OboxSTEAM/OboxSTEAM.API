using OboxSteam.Application.Commons;

namespace OboxSteam.Application.DTOs.DashboardDTO;

public class RevenueOverviewDto
{
    public decimal TotalRevenue { get; set; }

    public decimal RevenueInRange { get; set; }

    public decimal AverageOrderValue { get; set; }

    public int PendingPaymentRequestsCount { get; set; }

    public decimal PendingPaymentRequestsAmount { get; set; }

    public decimal RefundedAmount { get; set; }

    public int InvoiceCount { get; set; }

    public List<TrendPointDto> RevenueTrend { get; set; } = new();

    public List<RevenueByGatewayDto> RevenueByGateway { get; set; } = new();

    public Pagination<TopProgramRevenueDto> TopProgramsByRevenue { get; set; } = null!;
}
