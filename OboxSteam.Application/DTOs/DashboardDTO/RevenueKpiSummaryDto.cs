namespace OboxSteam.Application.DTOs.DashboardDTO;

public class RevenueKpiSummaryDto
{
    public decimal TotalRevenue { get; set; }

    public decimal RevenueInRange { get; set; }

    public int PendingPaymentRequestsCount { get; set; }

    public decimal PendingPaymentRequestsAmount { get; set; }

    public decimal RefundedAmount { get; set; }
}
