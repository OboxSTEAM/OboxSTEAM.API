namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class UpdatePortfolioSkillsRequestDto
{
    public List<UpdatePortfolioSkillEntryDto> Skills { get; set; } = [];
}

public class UpdatePortfolioSkillEntryDto
{
    public Guid SkillId { get; set; }

    public bool IsVisible { get; set; }

    public bool IsPinned { get; set; }

    public int DisplayOrder { get; set; }
}
