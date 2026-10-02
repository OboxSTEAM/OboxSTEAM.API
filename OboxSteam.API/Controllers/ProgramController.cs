using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.PaymentDTO;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/programs")]
[ApiController]
public class ProgramController : ControllerBase
{
    private readonly IProgramService _programService;
    private readonly IProgramApprovalService _programApprovalService;
    private const string ThreadsRemovedMessage =
        "Advisory threads were removed; use the program advisory discussion (advisory-discussion/messages, pins, read).";
    private const string ReviewFlowRemovedMessage =
        "The submission-based review flow was replaced by versioned approval; use approval/request, approval, approval/revoke, and publish.";
    private const string WorkspaceRemovedMessage =
        "The advisory board and timeline were replaced by the advisory workspace; use GET advisory and curriculum/changes.";
    private const string MentionsMovedMessage =
        "Advisory references and anchor fields were replaced by discussion mentions; use mention-targets and advisory-discussion/messages.";

    private readonly IProgramAdvisoryDiscussionService _programAdvisoryDiscussionService;
    private readonly IProgramAdvisoryAttachmentService _programAdvisoryAttachmentService;
    private readonly IEnrollmentCurriculumService _enrollmentCurriculumService;
    private readonly IClassService _classService;
    private readonly IClassSeatHoldService _classSeatHoldService;
    private readonly IRebuyClassCatalogService _rebuyClassCatalogService;

    public ProgramController(
        IProgramService programService,
        IProgramApprovalService programApprovalService,
        IProgramAdvisoryDiscussionService programAdvisoryDiscussionService,
        IProgramAdvisoryAttachmentService programAdvisoryAttachmentService,
        IEnrollmentCurriculumService enrollmentCurriculumService,
        IClassService classService,
        IClassSeatHoldService classSeatHoldService,
        IRebuyClassCatalogService rebuyClassCatalogService)
    {
        _programService = programService;
        _programApprovalService = programApprovalService;
        _programAdvisoryDiscussionService = programAdvisoryDiscussionService;
        _programAdvisoryAttachmentService = programAdvisoryAttachmentService;
        _enrollmentCurriculumService = enrollmentCurriculumService;
        _classService = classService;
        _classSeatHoldService = classSeatHoldService;
        _rebuyClassCatalogService = rebuyClassCatalogService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/programs
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all programs",
        Description = "Retrieve a paginated list of program information without modules. Supports search, filter, and sort options. "
            + "When status=Active (student catalog), only programs with at least one Standard Open class that still has seats "
            + "are returned — same enrollability rule as open-classes and tuition checkout.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramListItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetAllPrograms(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, level, rating, price, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by program code (optional)")] string? code = null,
        [FromQuery, SwaggerParameter(Description = "Filter by difficulty level (optional)")] DifficultyLevel? level = null,
        [FromQuery, SwaggerParameter(Description = "Filter by minimum rating (optional)")] decimal? rating = null,
        [FromQuery, SwaggerParameter(Description = "Filter by skills gained keyword (optional)")] string? skillsGained = null,
        [FromQuery, SwaggerParameter(Description = "Filter by program status: Draft, Approved, Active, Inactive (optional). Active also requires an enrollable Open Standard class with seats.")] ProgramStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Filter by category (optional)")] ProgramCategory? category = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _programService.GetAllProgramsAsync(
            search, sortBy, isDescending, page, pageSize,
            code, level, rating, skillsGained, status, category);

        return Ok(ApiResult<Pagination<ProgramListItemDto>>.Success(result, "200", "Programs retrieved successfully."));
    }

    // =========================================================================
    // GET ALL WITH MODULES  —  GET /api/programs/with-modules
    // =========================================================================

    [HttpGet("with-modules")]
    [SwaggerOperation(
        Summary = "Get all programs with modules",
        Description = "Retrieve a paginated list of programs including their modules. Supports search, filter, and sort options. "
            + "When status=Active, only programs with at least one Standard Open class that still has seats are returned.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramsResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetAllProgramsWithModules(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, level, rating, price, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by program code (optional)")] string? code = null,
        [FromQuery, SwaggerParameter(Description = "Filter by difficulty level (optional)")] DifficultyLevel? level = null,
        [FromQuery, SwaggerParameter(Description = "Filter by minimum rating (optional)")] decimal? rating = null,
        [FromQuery, SwaggerParameter(Description = "Filter by skills gained keyword (optional)")] string? skillsGained = null,
        [FromQuery, SwaggerParameter(Description = "Filter by program status: Draft, Approved, Active, Inactive (optional). Active also requires an enrollable Open Standard class with seats.")] ProgramStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Filter by category (optional)")] ProgramCategory? category = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _programService.GetAllProgramsWithModulesAsync(
            search, sortBy, isDescending, page, pageSize,
            code, level, rating, skillsGained, status, category);

        return Ok(ApiResult<Pagination<ProgramsResponseDto>>.Success(result, "200", "Programs with modules retrieved successfully."));
    }

    // =========================================================================
    // GET CURRICULUM  —  GET /api/programs/{id}/curriculum
    // =========================================================================

    [HttpGet("{id:guid}/curriculum")]
    [SwaggerOperation(
        Summary = "Get program curriculum tree",
        Description = "Retrieve a compact curriculum outline for a program: modules, courses or milestones, activities, and materials.")]
    [ProducesResponseType(typeof(ApiResult<ProgramCurriculumDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetProgramCurriculum([FromRoute] Guid id)
    {
        await _enrollmentCurriculumService.EnsureStudentEnrolledInProgramAsync(id);
        var result = await _programService.GetProgramCurriculumAsync(id);
        return Ok(ApiResult<ProgramCurriculumDto>.Success(result, "200", "Program curriculum retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/programs/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get program details",
        Description = "Retrieve detailed information for a specific program by its ID.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetProgramById([FromRoute] Guid id)
    {
        var result = await _programService.GetProgramByIdAsync(id);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program retrieved successfully."));
    }

    // =========================================================================
    // OPEN CLASSES FOR ENROLLMENT  —  GET /api/programs/{id}/open-classes
    // =========================================================================

    [HttpGet("{id:guid}/open-classes")]
    [SwaggerOperation(
        Summary = "List open classes available for enrollment",
        Description = "Public preview of Standard classes that are Open and still have seats, "
            + "including schedule sessions and seat counts. Use before checkout to show recruiting "
            + "cohorts. Call select-class when the learner picks a class to soft-hold a seat for 5 minutes.")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<OpenEnrollmentClassDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetOpenEnrollmentClasses(
        [FromRoute] Guid id,
        [FromQuery, SwaggerParameter(
            Description = "Optional class the learner viewed — soft-sorted first when still enrollable")]
        Guid? preferredClassId = null)
    {
        var result = await _classService.GetOpenEnrollmentClassesAsync(id, preferredClassId);
        return Ok(ApiResult<IReadOnlyList<OpenEnrollmentClassDto>>.Success(
            result,
            "200",
            "Open enrollment classes retrieved successfully."));
    }

    // =========================================================================
    // REBUY CLASSES  —  GET /api/programs/{id}/rebuy-classes  [Student]
    // =========================================================================

    [HttpGet("{id:guid}/rebuy-classes")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "List classes for first purchase or rebuy",
        Description = "Student picker for this program. First purchase, a Completed (100%) source, "
            + "and Failed/Dropped after the 3-month window return Open Standard classes with seats "
            + "(same join rule as open-classes; credit copy does not run after the window). Failed or "
            + "Dropped sources inside the window return Open and InProgress Standard classes with "
            + "per-module session progress and isEligible (stop-module / source-class rules). "
            + "IsRebuy is true only for Failed/Dropped inside the window. Active enrollment returns 409. "
            + "Public browse of recruiting cohorts still uses GET .../open-classes.")]
    [ProducesResponseType(typeof(ApiResult<RebuyClassCatalogDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> GetRebuyClasses([FromRoute] Guid id)
    {
        var result = await _rebuyClassCatalogService.GetRebuyClassesAsync(id);
        return Ok(ApiResult<RebuyClassCatalogDto>.Success(
            result,
            "200",
            "Rebuy classes retrieved successfully."));
    }

    // =========================================================================
    // SELECT CLASS  —  POST /api/programs/{id}/select-class
    // =========================================================================

    [HttpPost("{id:guid}/select-class")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Select a class and hold a seat",
        Description = "Starts a 5-minute soft seat hold when the student selects a class. "
            + "Checkout and parent-pay require this step first. Publishes seats.changed over SignalR. "
            + "When the program is not Active (for example it returned to Draft for re-approval), this, checkout, "
            + "and parent-pay release the student's hold and pending checkout and return 400 PROGRAM_NOT_AVAILABLE.")]
    [ProducesResponseType(typeof(ApiResult<SelectProgramClassResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SelectClassForCheckout(
        [FromRoute] Guid id,
        [FromBody] SelectProgramClassRequestDto dto)
    {
        var result = await _classSeatHoldService.SelectClassForCheckoutAsync(id, dto.ClassId);
        return Ok(ApiResult<SelectProgramClassResponseDto>.Success(
            result,
            "200",
            "Class selected and seat held for checkout."));
    }

    // =========================================================================
    // RELEASE CLASS HOLD  —  POST /api/programs/{id}/release-class-hold
    // =========================================================================

    [HttpPost("{id:guid}/release-class-hold")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Release checkout seat hold",
        Description = "Releases the student's soft seat hold and abandons the PendingPayment program enrollment "
            + "for this program. Call when the learner leaves the checkout page or reloads. Idempotent.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> ReleaseClassHoldForCheckout([FromRoute] Guid id)
    {
        await _classSeatHoldService.ReleaseClassHoldForCheckoutAsync(id);
        return Ok(ApiResult<object>.Success(null, "200", "Checkout seat hold released."));
    }

    // =========================================================================
    // GET BY NAME  —  GET /api/programs/name/{name}
    // =========================================================================

    [HttpGet("name/{name}")]
    [SwaggerOperation(
        Summary = "Get program by name",
        Description = "Retrieve a single program by its exact name (case-insensitive).")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetProgramByName(
        [FromRoute, SwaggerParameter(Description = "The program name to search for")] string name)
    {
        var result = await _programService.GetProgramByNameAsync(name);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program retrieved successfully."));
    }

    // =========================================================================
    // REVIEW QUEUE  —  GET /api/programs/review-queue
    // =========================================================================

    [Obsolete("The review queue was removed; use advisory-mine.")]
    [HttpGet("review-queue")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetReviewQueue()
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    [Obsolete("Curriculum review rounds were replaced by versioned approval.")]
    [HttpGet("{id:guid}/curriculum-reviews")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetCurriculumReviews([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    [Obsolete("Replaced by POST approval/request.")]
    [HttpPost("{id:guid}/submit-review")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult SubmitForReview([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    [Obsolete("Replaced by POST approval/revoke.")]
    [HttpPost("{id:guid}/withdraw-review")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult WithdrawReview([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    [HttpPut("{id:guid}/advisor")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Assign the responsible program expert",
        Description = "The expert must have an active linked login and is added to the program board. Allowed in Draft and Approved; changing the advisor of an Approved program revokes the approval (AdvisorChanged) and returns it to Draft. Posts AdvisorChanged. Active/Inactive return 409 INVALID_STATUS.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> AssignAdvisor(
        [FromRoute] Guid id,
        [FromBody] AssignProgramAdvisorRequest request)
    {
        var result = await _programApprovalService.AssignAdvisorAsync(id, request);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Responsible expert assigned."));
    }

    [HttpPost("{id:guid}/approval/request")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Ask the advisor to approve the current curriculum",
        Description = "Programs without a framework have no approval step (409 FRAMEWORK_REQUIRED). Draft only (409 INVALID_STATUS). Requires a responsible advisor (400 ADVISOR_REQUIRED) with an active login (400 ADVISOR_LOGIN_REQUIRED). Posts ApprovalRequested and notifies the advisor. Status is unchanged.")]
    [ProducesResponseType(typeof(ApiResult<ProgramAdvisoryWorkspaceDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> RequestApproval([FromRoute] Guid id)
    {
        var result = await _programApprovalService.RequestApprovalAsync(id);
        return Ok(ApiResult<ProgramAdvisoryWorkspaceDto>.Success(result, "200", "Approval requested."));
    }

    [HttpPost("{id:guid}/approval")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(
        Summary = "Approve the current curriculum version (advisor)",
        Description = "Checks in order: caller is the advisor (403); the program has a framework (409 FRAMEWORK_REQUIRED); status Draft (409 INVALID_STATUS); curriculumVersion matches (409 CURRICULUM_VERSION_STALE); no Open pins (409 APPROVAL_BLOCKED); framework check passes (409 FRAMEWORK_CHECK_FAILED with FrameworkCheckDto in value.data). Resolves Addressed pins, sets Approved, posts Approved, and notifies managers.")]
    [ProducesResponseType(typeof(ApiResult<ProgramAdvisoryWorkspaceDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<FrameworkCheckDto>), 409)]
    public async Task<IActionResult> ApproveProgram(
        [FromRoute] Guid id,
        [FromBody] ApproveProgramRequest request)
    {
        var result = await _programApprovalService.ApproveAsync(id, request);
        return Ok(ApiResult<ProgramAdvisoryWorkspaceDto>.Success(result, "200", "Program approved."));
    }

    [HttpPost("{id:guid}/approval/revoke")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Revoke the active approval",
        Description = "Manager/Admin (ManagerReopened, notifies the advisor) or the advisor (ExpertRevoked, notifies managers). Approved only (409 INVALID_STATUS). Returns the program to Draft and posts ApprovalRevoked.")]
    [ProducesResponseType(typeof(ApiResult<ProgramAdvisoryWorkspaceDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> RevokeApproval(
        [FromRoute] Guid id,
        [FromBody] RevokeProgramApprovalRequest? request = null)
    {
        var result = await _programApprovalService.RevokeAsync(id, request);
        return Ok(ApiResult<ProgramAdvisoryWorkspaceDto>.Success(result, "200", "Approval revoked."));
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Publish a program",
        Description = "With a framework: Approved only (409 INVALID_STATUS) and the active approval must cover the current curriculumVersion (409 CURRICULUM_VERSION_STALE). "
            + "Without a framework: no approval step; Draft (or legacy Approved) publishes directly, other statuses return 409 INVALID_STATUS. "
            + "Moves to Active, posts Published, and notifies managers. Enrollment and class creation require Active.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> PublishProgram([FromRoute] Guid id)
    {
        var result = await _programApprovalService.PublishAsync(id);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program published successfully."));
    }

    [HttpPost("{id:guid}/framework-version")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Upgrade to a newer version of the program's framework",
        Description = "Programs without a framework return 409 FRAMEWORK_REQUIRED. The version must be published, belong to the same framework, and be newer than the pinned one (400 FRAMEWORK_VERSION_INVALID). "
            + "Uses the curriculum cohort lock (409 CURRICULUM_LOCKED_COHORT). Pins the version, revokes the active approval (FrameworkUpgraded), "
            + "returns Approved/Active/Inactive programs to Draft, posts FrameworkUpgraded { fromVersion, toVersion }, and notifies the advisor. "
            + "Returns the workspace; frameworkCheckPassed reflects the new rules.")]
    [ProducesResponseType(typeof(ApiResult<ProgramAdvisoryWorkspaceDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpgradeFrameworkVersion(
        [FromRoute] Guid id,
        [FromBody] UpgradeProgramFrameworkVersionRequest request)
    {
        var result = await _programApprovalService.UpgradeFrameworkVersionAsync(id, request);
        return Ok(ApiResult<ProgramAdvisoryWorkspaceDto>.Success(result, "200", "Framework version upgraded."));
    }

    [Obsolete("Replaced by POST approval.")]
    [HttpPost("{id:guid}/approve-review")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult ApproveReview([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    [Obsolete("Replaced by pinned discussion messages.")]
    [HttpPost("{id:guid}/request-changes")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult RequestChanges([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    // =========================================================================
    // ADVISORY WORKSPACE
    // =========================================================================

    [HttpGet("advisory-mine")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Assigned advisory programs for the current user",
        Description = "Experts see programs where they are the advisor or a board member; managers and admins see all. Each item has status, approvalState, unreadCount (chat read cursor), openPinCount, and latestActivityAt. unreadOnly keeps items with unreadCount > 0.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<AdvisoryMineItemDto>>), 200)]
    public async Task<IActionResult> GetAdvisoryMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] ProgramStatus? status = null,
        [FromQuery] bool unreadOnly = false)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _programApprovalService.GetAdvisoryMineAsync(page, pageSize, status, unreadOnly);
        return Ok(ApiResult<Pagination<AdvisoryMineItemDto>>.Success(
            result, "200", "Advisory programs retrieved successfully."));
    }

    [HttpGet("{id:guid}/advisory")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Advisory workspace summary for a program",
        Description = "Status, curriculumVersion, frameworkVersionNumber, latestFrameworkVersionNumber, hasNewerFrameworkVersion, participants, capabilities, the active approval, pin counts, discussion unreadCount, live frameworkCheckPassed, changesSinceApprovalCount, unseenChangeCount, and latestSequence. "
            + "Only the advisor gets canPin/canResolvePin. Programs without a framework never get canRequestApproval/canApprove and get canPublish in Draft.")]
    [ProducesResponseType(typeof(ApiResult<ProgramAdvisoryWorkspaceDto>), 200)]
    public async Task<IActionResult> GetAdvisoryWorkspace([FromRoute] Guid id)
    {
        var result = await _programApprovalService.GetWorkspaceAsync(id);
        return Ok(ApiResult<ProgramAdvisoryWorkspaceDto>.Success(
            result, "200", "Advisory workspace retrieved successfully."));
    }

    [Obsolete("Replaced by GET advisory.")]
    [HttpGet("{id:guid}/advisory/timeline")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = WorkspaceRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetAdvisoryTimeline([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(WorkspaceRemovedMessage);

    [Obsolete("Replaced by GET advisory and curriculum/changes.")]
    [HttpGet("{id:guid}/advisory/board")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = WorkspaceRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetAdvisoryBoard([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(WorkspaceRemovedMessage);

    [HttpGet("{id:guid}/framework-check")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Structured framework checks for a program",
        Description = "Advisory participants only (Manager/Admin, the advisor, board experts).")]
    [ProducesResponseType(typeof(ApiResult<FrameworkCheckDto>), 200)]
    public async Task<IActionResult> GetFrameworkCheck([FromRoute] Guid id)
    {
        var result = await _programApprovalService.GetFrameworkCheckAsync(id);
        return Ok(ApiResult<FrameworkCheckDto>.Success(result, "200", "Framework check retrieved successfully."));
    }

    [Obsolete("Replaced by advisory-discussion/messages.")]
    [HttpGet("{id:guid}/advisory-threads")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetAdvisoryThreads([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [Obsolete("Replaced by advisory-discussion/messages.")]
    [HttpGet("{id:guid}/advisory-threads/{threadId:guid}")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetAdvisoryThread([FromRoute] Guid id, [FromRoute] Guid threadId)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [Obsolete("Replaced by advisory-discussion/pins and advisory-discussion/mention-counts.")]
    [HttpGet("{id:guid}/advisory-threads/pins")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetAdvisoryThreadPins([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [Obsolete("Advisory threads were removed; use advisory-discussion/messages.")]
    [HttpPost("{id:guid}/advisory-threads")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult CreateAdvisoryThread([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [Obsolete("Replaced by advisory-discussion/messages.")]
    [HttpGet("{id:guid}/advisory-threads/{threadId:guid}/messages")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetAdvisoryMessages([FromRoute] Guid id, [FromRoute] Guid threadId)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [Obsolete("Advisory threads were removed; use advisory-discussion/messages.")]
    [HttpPost("{id:guid}/advisory-threads/{threadId:guid}/messages")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult AddAdvisoryMessage([FromRoute] Guid id, [FromRoute] Guid threadId)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [Obsolete("Advisory threads were removed; use advisory-discussion pins.")]
    [HttpPost("{id:guid}/advisory-threads/{threadId:guid}/actions")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult PerformAdvisoryThreadAction([FromRoute] Guid id, [FromRoute] Guid threadId)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [Obsolete("Advisory threads were removed; use advisory-discussion pins.")]
    [HttpPatch("{id:guid}/advisory-threads/{threadId:guid}/status")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult UpdateAdvisoryThreadStatus([FromRoute] Guid id, [FromRoute] Guid threadId)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [Obsolete("Replaced by advisory-discussion/read.")]
    [HttpPost("{id:guid}/advisory-read")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult RecordAdvisoryRead([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [Obsolete("Mentions are captured from advisory-discussion message bodies.")]
    [HttpPost("{id:guid}/advisory-references")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = MentionsMovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult CreateAdvisoryReference([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(MentionsMovedMessage);

    [Obsolete("Mentions are resolved inside advisory-discussion messages.")]
    [HttpGet("{id:guid}/advisory-references/{referenceId:guid}")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = MentionsMovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetAdvisoryReference([FromRoute] Guid id, [FromRoute] Guid referenceId)
        => throw RemovedEndpoint.Gone(MentionsMovedMessage);

    [Obsolete("Replaced by mention-targets.")]
    [HttpGet("advisory-anchor-fields")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = MentionsMovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetAdvisoryAnchorFields()
        => throw RemovedEndpoint.Gone(MentionsMovedMessage);

    [HttpGet("{id:guid}/mention-targets")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "List mentionable curriculum components",
        Description = "Participants only. Every component of the program (program, modules, courses, research milestones, activities, materials, assignments) as a flat list in curriculum tree order, with ancestor path.")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<MentionTargetDto>>), 200)]
    public async Task<IActionResult> GetMentionTargets([FromRoute] Guid id)
    {
        var result = await _programAdvisoryDiscussionService.GetMentionTargetsAsync(id);
        return Ok(ApiResult<IReadOnlyList<MentionTargetDto>>.Success(result, "200", "Mention targets retrieved."));
    }

    [HttpGet("{id:guid}/advisory-discussion/messages")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "List paginated program chat messages",
        Description = "Cursor paging with before/after. targetType + targetId (together) return only messages that mention that exact component. Deleted messages are tombstones.")]
    [ProducesResponseType(typeof(ApiResult<AdvisoryDiscussionPageDto>), 200)]
    public async Task<IActionResult> GetAdvisoryDiscussionMessages(
        [FromRoute] Guid id,
        [FromQuery] string? before = null,
        [FromQuery] string? after = null,
        [FromQuery] int pageSize = 30,
        [FromQuery] ProgramAdvisoryTargetType? targetType = null,
        [FromQuery] Guid? targetId = null)
    {
        var result = await _programAdvisoryDiscussionService.GetMessagesAsync(
            id, before, after, pageSize, targetType, targetId);
        return Ok(ApiResult<AdvisoryDiscussionPageDto>.Success(result, "200", "Discussion messages retrieved successfully."));
    }

    [HttpGet("{id:guid}/advisory-discussion/messages/{messageId:guid}")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Get one program chat message")]
    [ProducesResponseType(typeof(ApiResult<AdvisoryDiscussionMessageDto>), 200)]
    public async Task<IActionResult> GetAdvisoryDiscussionMessage(
        [FromRoute] Guid id,
        [FromRoute] Guid messageId)
    {
        var result = await _programAdvisoryDiscussionService.GetMessageAsync(id, messageId);
        return Ok(ApiResult<AdvisoryDiscussionMessageDto>.Success(result, "200", "Discussion message retrieved successfully."));
    }

    [HttpPost("{id:guid}/advisory-discussion/messages")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Post a program chat message",
        Description = "Text may contain mention tokens @[Type:uuid] (≤ 20, each must belong to the program: 400 MENTION_TARGET_INVALID). Text ≤ 4000 (MESSAGE_TOO_LONG); text or attachments required (MESSAGE_EMPTY); ≤ 10 unsent own attachments (TOO_MANY_ATTACHMENTS, ATTACHMENT_INVALID). Idempotent on author + clientMessageId.")]
    [ProducesResponseType(typeof(ApiResult<AdvisoryDiscussionMessageDto>), 200)]
    public async Task<IActionResult> AddAdvisoryDiscussionMessage(
        [FromRoute] Guid id,
        [FromBody] PostAdvisoryDiscussionMessageRequest request)
    {
        var result = await _programAdvisoryDiscussionService.AddMessageAsync(id, request);
        return Ok(ApiResult<AdvisoryDiscussionMessageDto>.Success(result, "200", "Discussion message added."));
    }

    [HttpPatch("{id:guid}/advisory-discussion/messages/{messageId:guid}")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Edit own chat message", Description = "Author only. Mentions are re-parsed; same text rules as posting.")]
    [ProducesResponseType(typeof(ApiResult<AdvisoryDiscussionMessageDto>), 200)]
    public async Task<IActionResult> EditAdvisoryDiscussionMessage(
        [FromRoute] Guid id,
        [FromRoute] Guid messageId,
        [FromBody] EditAdvisoryDiscussionMessageRequest request)
    {
        var result = await _programAdvisoryDiscussionService.EditMessageAsync(id, messageId, request);
        return Ok(ApiResult<AdvisoryDiscussionMessageDto>.Success(result, "200", "Discussion message updated."));
    }

    [HttpDelete("{id:guid}/advisory-discussion/messages/{messageId:guid}")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Delete own chat message", Description = "Author only. The message stays as a tombstone; mentions, attachments, and pin are removed.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    public async Task<IActionResult> RemoveAdvisoryDiscussionMessage(
        [FromRoute] Guid id,
        [FromRoute] Guid messageId)
    {
        await _programAdvisoryDiscussionService.RemoveMessageAsync(id, messageId);
        return Ok(ApiResult<object>.Success(new { }, "200", "Discussion message deleted."));
    }

    [HttpPost("{id:guid}/advisory-discussion/messages/{messageId:guid}/pin")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Pin a chat message", Description = "Program advisor only (board experts and managers get 403). Pin starts Open; already pinned messages are returned unchanged.")]
    [ProducesResponseType(typeof(ApiResult<AdvisoryDiscussionMessageDto>), 200)]
    public async Task<IActionResult> PinAdvisoryDiscussionMessage(
        [FromRoute] Guid id,
        [FromRoute] Guid messageId)
    {
        var result = await _programAdvisoryDiscussionService.PinMessageAsync(id, messageId);
        return Ok(ApiResult<AdvisoryDiscussionMessageDto>.Success(result, "200", "Message pinned."));
    }

    [HttpDelete("{id:guid}/advisory-discussion/messages/{messageId:guid}/pin")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Unpin a chat message", Description = "Program advisor only (board experts and managers get 403).")]
    [ProducesResponseType(typeof(ApiResult<AdvisoryDiscussionMessageDto>), 200)]
    public async Task<IActionResult> UnpinAdvisoryDiscussionMessage(
        [FromRoute] Guid id,
        [FromRoute] Guid messageId)
    {
        var result = await _programAdvisoryDiscussionService.UnpinMessageAsync(id, messageId);
        return Ok(ApiResult<AdvisoryDiscussionMessageDto>.Success(result, "200", "Message unpinned."));
    }

    [HttpPost("{id:guid}/advisory-discussion/messages/{messageId:guid}/pin/actions")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Change a pin status",
        Description = "MarkAddressed (Manager/Admin, Open → Addressed), Reopen (program advisor, Addressed/Resolved → Open), Resolve (program advisor, Open/Addressed → Resolved). Board experts get 403. Invalid transitions return 409 INVALID_STATUS.")]
    [ProducesResponseType(typeof(ApiResult<AdvisoryDiscussionMessageDto>), 200)]
    public async Task<IActionResult> PerformAdvisoryPinAction(
        [FromRoute] Guid id,
        [FromRoute] Guid messageId,
        [FromBody] AdvisoryDiscussionPinActionRequest request)
    {
        var result = await _programAdvisoryDiscussionService.PerformPinActionAsync(id, messageId, request);
        return Ok(ApiResult<AdvisoryDiscussionMessageDto>.Success(result, "200", "Pin updated."));
    }

    [HttpGet("{id:guid}/advisory-discussion/pins")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "List pinned chat messages", Description = "Ordered by pinnedAt; optional status filter.")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<AdvisoryDiscussionMessageDto>>), 200)]
    public async Task<IActionResult> GetAdvisoryDiscussionPins(
        [FromRoute] Guid id,
        [FromQuery] DiscussionPinStatus? status = null)
    {
        var result = await _programAdvisoryDiscussionService.GetPinsAsync(id, status);
        return Ok(ApiResult<IReadOnlyList<AdvisoryDiscussionMessageDto>>.Success(result, "200", "Pinned messages retrieved."));
    }

    [HttpGet("{id:guid}/advisory-discussion/mention-counts")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Mention counts per component", Description = "Exact target only (no roll-up to ancestors). Deleted messages are excluded.")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<AdvisoryMentionCountDto>>), 200)]
    public async Task<IActionResult> GetAdvisoryMentionCounts([FromRoute] Guid id)
    {
        var result = await _programAdvisoryDiscussionService.GetMentionCountsAsync(id);
        return Ok(ApiResult<IReadOnlyList<AdvisoryMentionCountDto>>.Success(result, "200", "Mention counts retrieved."));
    }

    [HttpPost("{id:guid}/advisory-discussion/attachments")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [RequestSizeLimit(25L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 25L * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Upload a chat attachment",
        Description = "Multipart field 'file'. ≤ 20 MB (400 ATTACHMENT_TOO_LARGE). Allowed: png, jpg, jpeg, gif, webp, pdf, doc, docx, ppt, pptx, xls, xlsx, zip (400 ATTACHMENT_TYPE_NOT_ALLOWED). The attachment stays unsent until a message references it; unsent uploads are purged after 24 hours.")]
    [ProducesResponseType(typeof(ApiResult<AdvisoryDiscussionAttachmentDto>), 200)]
    public async Task<IActionResult> UploadAdvisoryDiscussionAttachment(
        [FromRoute] Guid id,
        IFormFile file)
    {
        var result = await _programAdvisoryAttachmentService.UploadAsync(id, file);
        return Ok(ApiResult<AdvisoryDiscussionAttachmentDto>.Success(result, "200", "Attachment uploaded."));
    }

    [HttpGet("{id:guid}/advisory-discussion/attachments/{attachmentId:guid}/url")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Get a chat attachment download URL", Description = "Presigned URL valid for 15 minutes. Unsent attachments are visible to their uploader only.")]
    [ProducesResponseType(typeof(ApiResult<AdvisoryAttachmentUrlDto>), 200)]
    public async Task<IActionResult> GetAdvisoryDiscussionAttachmentUrl(
        [FromRoute] Guid id,
        [FromRoute] Guid attachmentId)
    {
        var result = await _programAdvisoryAttachmentService.GetUrlAsync(id, attachmentId);
        return Ok(ApiResult<AdvisoryAttachmentUrlDto>.Success(result, "200", "Attachment URL created."));
    }

    [Obsolete("Replaced by advisory-discussion/read.")]
    [HttpPost("{id:guid}/advisory-threads/{threadId:guid}/read")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ThreadsRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult RecordAdvisoryThreadRead([FromRoute] Guid id, [FromRoute] Guid threadId)
        => throw RemovedEndpoint.Gone(ThreadsRemovedMessage);

    [HttpPost("{id:guid}/advisory-discussion/read")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Advance the program Discussion read cursor")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    public async Task<IActionResult> RecordAdvisoryDiscussionRead(
        [FromRoute] Guid id,
        [FromBody] RecordAdvisoryDiscussionReadRequest request)
    {
        await _programAdvisoryDiscussionService.RecordDiscussionReadAsync(id, request);
        return Ok(ApiResult<object>.Success(new { }, "200", "Discussion read cursor recorded."));
    }

    [Obsolete("Review submissions were replaced by versioned approval.")]
    [HttpGet("{id:guid}/review-submissions")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetReviewSubmissions([FromRoute] Guid id)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    [Obsolete("Review submissions were replaced by versioned approval.")]
    [HttpGet("{id:guid}/review-submissions/{submissionId:guid}")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetReviewSubmission([FromRoute] Guid id, [FromRoute] Guid submissionId)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    [Obsolete("Replaced by curriculum/changes.")]
    [HttpGet("{id:guid}/review-submissions/{submissionId:guid}/changes")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetReviewSubmissionChanges([FromRoute] Guid id, [FromRoute] Guid submissionId)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    [Obsolete("Review drafts were removed with the submission-based review flow.")]
    [HttpGet("{id:guid}/review-submissions/{submissionId:guid}/draft")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult GetReviewDraft([FromRoute] Guid id, [FromRoute] Guid submissionId)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    [Obsolete("Review drafts were removed with the submission-based review flow.")]
    [HttpPut("{id:guid}/review-submissions/{submissionId:guid}/draft")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(Summary = "Removed (410 ENDPOINT_REMOVED)", Description = ReviewFlowRemovedMessage)]
    [ProducesResponseType(typeof(ApiResult<object>), 410)]
    public IActionResult SaveReviewDraft([FromRoute] Guid id, [FromRoute] Guid submissionId)
        => throw RemovedEndpoint.Gone(ReviewFlowRemovedMessage);

    // =========================================================================
    // CREATE  —  POST /api/programs          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Create a new program",
        Description = "Creates a new program as Draft. Use the approval endpoints and publish to change status. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> AddProgram(
        [FromForm, SwaggerParameter("Program data to be created (multipart field prefix: data.<PropertyName>)")] CreateProgramRequestDto data,
        IFormFile file)
    {
        var result = await _programService.CreateProgramAsync(data, file);

        return CreatedAtAction(
            nameof(GetProgramById),
            new { id = result.Id },
            ApiResult<ProgramsResponseDto>.Success(result, "201", "Program created successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/programs/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update program information",
        Description = "Updates program details. Status may only toggle Active ↔ Inactive. The framework is set only on create: "
            + "frameworkId, frameworkVersionId, or clearFramework that would change it return 409 FRAMEWORK_LOCKED "
            + "(omitted or matching values are ignored). Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> UpdateProgram(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated program data")] UpdateProgramRequestDto dto)
    {
        if (dto == null)
            return BadRequest(ApiResult<object>.Failure("400", "Program update data is required."));

        var result = await _programService.UpdateProgramAsync(id, dto);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program updated successfully."));
    }

    /// <summary>
    /// Upload thumbnail for a specific program.
    /// </summary>
    /// <param name="id">Program ID.</param>
    /// <param name="file">Image file (jpg, jpeg, png, webp). Max 5 MB.</param>
    /// <returns>Updated program with new thumbnail URL.</returns>
    [HttpPost("{id:guid}/thumbnail")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Upload program thumbnail",
        Description = "Uploads a new thumbnail image for the specified program. Replaces the existing thumbnail if one exists.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadProgramThumbnail([FromRoute] Guid id, IFormFile file)
    {
        var result = await _programService.UploadProgramThumbnailAsync(id, file);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program thumbnail uploaded successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/programs/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a program",
        Description = "Soft-deletes a program by its ID. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> DeleteProgram([FromRoute] Guid id)
    {
        var result = await _programService.DeleteProgramAsync(id);

        if (!result)
            return NotFound(ApiResult<object>.Failure("404", $"Program with ID '{id}' not found."));

        return Ok(ApiResult<bool>.Success(result, "200", "Program deleted successfully."));
    }
}
