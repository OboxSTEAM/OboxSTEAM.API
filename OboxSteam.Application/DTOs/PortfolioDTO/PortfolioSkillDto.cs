namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class PortfolioSkillDto : StudentSkillDto
{
    public bool IsVisible { get; set; }

    public bool IsPinned { get; set; }

    public int DisplayOrder { get; set; }
}
