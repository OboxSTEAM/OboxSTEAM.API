namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryCapabilitiesDto
{
    public bool CanPost { get; set; }
    public bool CanPin { get; set; }
    public bool CanResolvePin { get; set; }
    public bool CanEditCurriculum { get; set; }
    public bool CanApprove { get; set; }
    public bool CanRevokeApproval { get; set; }
    public bool CanRequestApproval { get; set; }
    public bool CanPublish { get; set; }
    public bool CanUpgradeFrameworkVersion { get; set; }
}
