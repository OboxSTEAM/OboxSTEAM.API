using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.DashboardDTO;

public class DashboardFilterDto
{
    public DashboardRange Range { get; set; } = DashboardRange.Last30Days;

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public Guid? ProgramId { get; set; }

    public Guid? ModuleId { get; set; }

    public Guid? ClassId { get; set; }

    public PaymentStatus? PaymentStatus { get; set; }

    public EnrollmentStatus? EnrollmentStatus { get; set; }

    public ClassEnrollmentStatus? ClassEnrollmentStatus { get; set; }

    public SubmissionStatus? SubmissionStatus { get; set; }

    public ClassStatus? ClassStatus { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 5;

    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
}
