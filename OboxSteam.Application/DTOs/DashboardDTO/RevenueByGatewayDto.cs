using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.DashboardDTO;

public class RevenueByGatewayDto
{
    public PaymentGateway Gateway { get; set; }

    public decimal Amount { get; set; }
}
