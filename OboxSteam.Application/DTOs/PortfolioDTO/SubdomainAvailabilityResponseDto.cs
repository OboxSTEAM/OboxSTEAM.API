namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class SubdomainAvailabilityResponseDto
{
    public string Subdomain { get; set; } = null!;

    public bool Available { get; set; }

    public string? Reason { get; set; }
}
