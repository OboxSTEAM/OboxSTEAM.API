using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.DashboardDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/dashboard")]
[ApiController]
[Authorize(Roles = "Admin,Manager")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("overview")]
    [SwaggerOperation(
        Summary = "Manager dashboard trimmed KPIs",
        Description = "Returns trimmed KPI summaries for revenue, enrollment, assessment, and operations (no trends / top-N). Prefer /landing for the home page to avoid duplicate section fetches. Status filters that do not apply to a section are ignored.")]
    [ProducesResponseType(typeof(ApiResult<DashboardOverviewDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetOverview(
        [FromQuery] DashboardRange range = DashboardRange.Last30Days,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? programId = null,
        [FromQuery] Guid? moduleId = null,
        [FromQuery] Guid? classId = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] EnrollmentStatus? enrollmentStatus = null,
        [FromQuery] ClassEnrollmentStatus? classEnrollmentStatus = null,
        [FromQuery] SubmissionStatus? submissionStatus = null,
        [FromQuery] ClassStatus? classStatus = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _dashboardService.GetOverviewAsync(
            BuildFilter(
                range, fromDate, toDate, programId, moduleId, classId,
                paymentStatus, enrollmentStatus, classEnrollmentStatus,
                submissionStatus, classStatus, page, pageSize, sortBy, isDescending));

        return Ok(ApiResult<DashboardOverviewDto>.Success(
            result, "200", "Dashboard overview retrieved successfully."));
    }

    [HttpGet("landing")]
    [SwaggerOperation(
        Summary = "Manager dashboard landing (single request)",
        Description = "Returns full revenue, enrollment, assessment, and operations sections (KPIs + trends + top-N) in one response so the landing page does not call each section endpoint separately.")]
    [ProducesResponseType(typeof(ApiResult<DashboardLandingDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetLanding(
        [FromQuery] DashboardRange range = DashboardRange.Last30Days,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? programId = null,
        [FromQuery] Guid? moduleId = null,
        [FromQuery] Guid? classId = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] EnrollmentStatus? enrollmentStatus = null,
        [FromQuery] ClassEnrollmentStatus? classEnrollmentStatus = null,
        [FromQuery] SubmissionStatus? submissionStatus = null,
        [FromQuery] ClassStatus? classStatus = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _dashboardService.GetLandingAsync(
            BuildFilter(
                range, fromDate, toDate, programId, moduleId, classId,
                paymentStatus, enrollmentStatus, classEnrollmentStatus,
                submissionStatus, classStatus, page, pageSize, sortBy, isDescending));

        return Ok(ApiResult<DashboardLandingDto>.Success(
            result, "200", "Dashboard landing retrieved successfully."));
    }

    [HttpGet("revenue")]
    [SwaggerOperation(
        Summary = "Manager dashboard revenue section",
        Description = "Honors date range, programId/moduleId/classId, paymentStatus, and pagination for top programs. Other status filters are ignored.")]
    [ProducesResponseType(typeof(ApiResult<RevenueOverviewDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] DashboardRange range = DashboardRange.Last30Days,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? programId = null,
        [FromQuery] Guid? moduleId = null,
        [FromQuery] Guid? classId = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] EnrollmentStatus? enrollmentStatus = null,
        [FromQuery] ClassEnrollmentStatus? classEnrollmentStatus = null,
        [FromQuery] SubmissionStatus? submissionStatus = null,
        [FromQuery] ClassStatus? classStatus = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _dashboardService.GetRevenueOverviewAsync(
            BuildFilter(
                range, fromDate, toDate, programId, moduleId, classId,
                paymentStatus, enrollmentStatus, classEnrollmentStatus,
                submissionStatus, classStatus, page, pageSize, sortBy, isDescending));

        return Ok(ApiResult<RevenueOverviewDto>.Success(
            result, "200", "Revenue overview retrieved successfully."));
    }

    [HttpGet("enrollment")]
    [SwaggerOperation(
        Summary = "Manager dashboard enrollment section",
        Description = "Honors date range, programId/moduleId/classId, enrollmentStatus, classEnrollmentStatus, and pagination for top programs. Other status filters are ignored.")]
    [ProducesResponseType(typeof(ApiResult<EnrollmentOverviewDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetEnrollment(
        [FromQuery] DashboardRange range = DashboardRange.Last30Days,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? programId = null,
        [FromQuery] Guid? moduleId = null,
        [FromQuery] Guid? classId = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] EnrollmentStatus? enrollmentStatus = null,
        [FromQuery] ClassEnrollmentStatus? classEnrollmentStatus = null,
        [FromQuery] SubmissionStatus? submissionStatus = null,
        [FromQuery] ClassStatus? classStatus = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _dashboardService.GetEnrollmentOverviewAsync(
            BuildFilter(
                range, fromDate, toDate, programId, moduleId, classId,
                paymentStatus, enrollmentStatus, classEnrollmentStatus,
                submissionStatus, classStatus, page, pageSize, sortBy, isDescending));

        return Ok(ApiResult<EnrollmentOverviewDto>.Success(
            result, "200", "Enrollment overview retrieved successfully."));
    }

    [HttpGet("assessment")]
    [SwaggerOperation(
        Summary = "Manager dashboard assessment section",
        Description = "Honors date range, programId/moduleId/classId, and submissionStatus. Other status filters and pagination are ignored.")]
    [ProducesResponseType(typeof(ApiResult<AssessmentOverviewDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetAssessment(
        [FromQuery] DashboardRange range = DashboardRange.Last30Days,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? programId = null,
        [FromQuery] Guid? moduleId = null,
        [FromQuery] Guid? classId = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] EnrollmentStatus? enrollmentStatus = null,
        [FromQuery] ClassEnrollmentStatus? classEnrollmentStatus = null,
        [FromQuery] SubmissionStatus? submissionStatus = null,
        [FromQuery] ClassStatus? classStatus = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _dashboardService.GetAssessmentOverviewAsync(
            BuildFilter(
                range, fromDate, toDate, programId, moduleId, classId,
                paymentStatus, enrollmentStatus, classEnrollmentStatus,
                submissionStatus, classStatus, page, pageSize, sortBy, isDescending));

        return Ok(ApiResult<AssessmentOverviewDto>.Success(
            result, "200", "Assessment overview retrieved successfully."));
    }

    [HttpGet("operations")]
    [SwaggerOperation(
        Summary = "Manager dashboard operations section",
        Description = "Honors date range, programId/moduleId/classId, classStatus, and pagination for mentor utilization. Other status filters are ignored.")]
    [ProducesResponseType(typeof(ApiResult<OperationsOverviewDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetOperations(
        [FromQuery] DashboardRange range = DashboardRange.Last30Days,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? programId = null,
        [FromQuery] Guid? moduleId = null,
        [FromQuery] Guid? classId = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] EnrollmentStatus? enrollmentStatus = null,
        [FromQuery] ClassEnrollmentStatus? classEnrollmentStatus = null,
        [FromQuery] SubmissionStatus? submissionStatus = null,
        [FromQuery] ClassStatus? classStatus = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _dashboardService.GetOperationsOverviewAsync(
            BuildFilter(
                range, fromDate, toDate, programId, moduleId, classId,
                paymentStatus, enrollmentStatus, classEnrollmentStatus,
                submissionStatus, classStatus, page, pageSize, sortBy, isDescending));

        return Ok(ApiResult<OperationsOverviewDto>.Success(
            result, "200", "Operations overview retrieved successfully."));
    }

    private static DashboardFilterDto BuildFilter(
        DashboardRange range,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? programId,
        Guid? moduleId,
        Guid? classId,
        PaymentStatus? paymentStatus,
        EnrollmentStatus? enrollmentStatus,
        ClassEnrollmentStatus? classEnrollmentStatus,
        SubmissionStatus? submissionStatus,
        ClassStatus? classStatus,
        int page,
        int pageSize,
        string? sortBy,
        bool isDescending)
        => new()
        {
            Range = range,
            FromDate = fromDate,
            ToDate = toDate,
            ProgramId = programId,
            ModuleId = moduleId,
            ClassId = classId,
            PaymentStatus = paymentStatus,
            EnrollmentStatus = enrollmentStatus,
            ClassEnrollmentStatus = classEnrollmentStatus,
            SubmissionStatus = submissionStatus,
            ClassStatus = classStatus,
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            IsDescending = isDescending
        };
}
