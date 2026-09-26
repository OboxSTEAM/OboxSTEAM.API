using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public partial class SeedService : ISeedService
{
    private const string SeedS3Folder = "Seed";

    private readonly ILogger _loggerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;
    private readonly ICertificateService _certificateService;

    public SeedService(
        ILogger<SeedService> loggerService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        ICertificateService certificateService)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _certificateService = certificateService;
    }

    public async Task SeedAllDataAsync()
    {
        _loggerService.LogInformation("Starting seed all data");
        _seedNow = DateTime.UtcNow;

        using (await SeedExecutionGuard.BeginAsync())
        {
            await SeedAllDataCoreAsync();
        }

        _loggerService.LogInformation("Finished seed all data");
    }

    /// <summary>
    /// Single ordered seed pass. Order invariants:
    /// <list type="bullet">
    /// <item>Materials after FailRebuy so new activities get reading assets.</item>
    /// <item>Safety-net before AssignmentWindows so leftover-fail cannot AcademicFail mid-seed.</item>
    /// <item>Demo clear before Maker STD-010 so the theory quiz grade is not wiped.</item>
    /// <item>Research UI fixtures after the final elapsed-window pass so FileUpload rows keep ResearchMilestoneId.</item>
    /// <item>Maker joinable sessions after wall-clock realign / weekly grid so Sat/Sun does not overwrite them.</item>
    /// <item>Session experts once after Maker tail fixtures so joinable sessions get co-teach rows.</item>
    /// </list>
    /// </summary>
    private async Task SeedAllDataCoreAsync()
    {
        await SeedUsersAsync();
        await EnsureAdditionalMentorUsersAsync();
        await EnsureExpertUsersAsync();
        await SeedMentorProfilesAsync();
        await SeedExpertsAsync();
        await SeedExpertCredentialsAsync();
        await SeedProgramsAsync();
        await SeedProgramFrameworksAsync();
        await SeedProgramBoardsAsync();
        await SeedSkillsAsync();
        await SeedProgramSkillsAsync();
        await SeedModulesAsync();
        await SeedCoursesAsync();
        await SeedActivitiesAsync();
        await SeedParentStudentLinksAsync();
        await SeedProgramEnrollmentsAsync();
        await SeedModuleEnrollmentsAsync();
        await SeedCourseEnrollmentsAsync();
        await SeedRoboticsQuestionBanksAsync();
        await SeedAssignmentsAsync();
        await SeedMentorSkillsAsync();
        await SeedAcademicYearClassesAsync();
        await SeedMentorBoardClassesAsync();
        await AlignUnassignedClassesToReadyForMentorAsync();
        await SeedClassEnrollmentsAsync();
        await SeedAcademicYearSessionsAsync();
        // Re-run after activities/sessions exist so board classes created on older DBs get timetables.
        await EnsureMentorBoardPlaceholderSchedulesAsync(_seedNow);
        await SeedResearchMilestoneDataAsync();
        await SeedResearchModuleEnrollmentsAsync();
        await SeedResearchActivityProgressAsync();
        await SeedSessionAlignedActivityProgressAsync();
        await BackfillActivityProgressStatusAsync();
        await SeedResearchSubmissionsAsync();
        await SeedExtendedResearchDataAsync();
        await SeedDemoShowcaseProgramsAsync();
        await SeedReviewDraftProgramsAsync();
        await SeedExpertAdvisoryDemoAsync();
        await EnsureClassSessionCoverageAsync();
        await RealignSeedSessionWallClocksAsync();
        await SeedWeeklyScheduleFixtureAsync();
        await EnsureSeedSessionVenuesAsync();
        await SeedPortfolioDataAsync();
        // Demo clear must run before Maker STD-010 fixtures (quiz grade would be wiped).
        await ClearDemoProgramSubmissionsAsync();
        await SeedFailRebuyFixturesAsync();
        // After FailRebuy so newly created self-paced activities get materials.
        await SeedMaterialsAsync();
        // Safety-net before windows so leftover-fail cannot AcademicFail mid-seed.
        await SeedTaughtModuleAssessmentSafetyNetAsync();
        await EnsureAssignmentWorkWindowsAsync();
        await SeedPassedSubmissionsForElapsedRequiredWindowsAsync();
        await AlignInProgressCurriculumToClassTimetableAsync();
        await SeedCertTestProgressAsync();
        await SeedCompletedProgramCertificatesAsync();
        await SeedPaymentsAsync();
        await SeedProgramReviewsAsync();
        await SeedNotificationsAsync();
        await SeedExpertAdvisoryNotificationsAsync();
        await RestoreInProgressPurchasesClosedDuringSeedAsync();
        // Restore-repair only: remove holds on not-yet-open windows and cover reopened seats.
        await SeedTaughtModuleAssessmentSafetyNetAsync();
        await SeedPassedSubmissionsForElapsedRequiredWindowsAsync();
        // After final elapsed-related state: research FileUpload fixtures need ResearchMilestoneId.
        await SeedGradedCapstoneSubmissionForUiAsync();
        // After RealignSeedSessionWallClocksAsync / weekly grid so Sat/Sun does not overwrite Maker Slice-2.
        await ApplyMakerSlice2JoinableSessionsAsync();
        // After ClearDemoProgramSubmissionsAsync so STD-010 theory quiz grade is not wiped.
        await ApplyMakerStudent10Module1CompleteAsync();
        // Once after Maker tail fixtures so joinable sessions get co-teach expert rows.
        await SeedClassSessionExpertsAsync();
        await VerifySeedDemoIntegrityAsync();
    }

    public async Task ClearAllDataAsync()
    {
        _loggerService.LogInformation("Starting clear all data");

        await ClearS3ObjectsAsync();
        await _unitOfWork.TruncateAllApplicationTablesAsync();
        await VerifyDatabaseIsEmptyAsync();

        _loggerService.LogInformation("Finished clear all data");
    }

    private async Task VerifyDatabaseIsEmptyAsync()
    {
        await EnsureTableEmptyAsync(_unitOfWork.Users, "Users");
        await EnsureTableEmptyAsync(_unitOfWork.Programs, "Programs");
        await EnsureTableEmptyAsync(_unitOfWork.Activities, "Activities");
        await EnsureTableEmptyAsync(_unitOfWork.ResearchMilestones, "ResearchMilestones");
        await EnsureTableEmptyAsync(_unitOfWork.Submissions, "Submissions");
        await EnsureTableEmptyAsync(_unitOfWork.ProgramEnrollments, "ProgramEnrollments");
        await EnsureTableEmptyAsync(_unitOfWork.ClassEnrollments, "ClassEnrollments");
        await EnsureTableEmptyAsync(_unitOfWork.ClassSessions, "ClassSessions");
        await EnsureTableEmptyAsync(_unitOfWork.Payments, "Payments");
        await EnsureTableEmptyAsync(_unitOfWork.Certificates, "Certificates");
        await EnsureTableEmptyAsync(_unitOfWork.Portfolios, "Portfolios");
        await EnsureTableEmptyAsync(_unitOfWork.ProgramSkills, "ProgramSkills");
        await EnsureTableEmptyAsync(_unitOfWork.PortfolioSkills, "PortfolioSkills");
        await EnsureTableEmptyAsync(_unitOfWork.Notifications, "Notifications");
    }

    private static async Task EnsureTableEmptyAsync<TEntity>(
        IGenericRepository<TEntity> repository,
        string tableName)
        where TEntity : Domain.Entities.BaseEntity
    {
        if (await repository.AnyIncludingDeletedAsync())
        {
            throw ErrorHelper.Internal($"Clear incomplete: {tableName} table still has rows.");
        }
    }
}
