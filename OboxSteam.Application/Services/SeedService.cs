using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Commons.CurriculumChanges;
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
    private readonly IFaceRecognitionService _faceRecognitionService;

    public SeedService(
        ILogger<SeedService> loggerService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        ICertificateService certificateService,
        IFaceRecognitionService faceRecognitionService)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _certificateService = certificateService;
        _faceRecognitionService = faceRecognitionService;
    }

    public async Task SeedAllDataAsync()
    {
        _loggerService.LogInformation("Starting seed all data");
        _seedNow = DateTime.UtcNow;

        using (await SeedExecutionGuard.BeginAsync())
        using (CurriculumChangeScope.Suppress())
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
    /// <item>Capstone live pair pinned after wall-clock realign / weekly grid / venues so Sat/Sun does not overwrite it,
    /// and before AssignmentWindows so work windows follow the pinned times.</item>
    /// <item>Demo clear before the capstone class journey so Module 1 quiz grades are not wiped.</item>
    /// <item>Research UI fixtures after the final elapsed-window pass so FileUpload rows keep ResearchMilestoneId.</item>
    /// <item>Session experts once at the end so the pinned capstone Offline gets its co-teach rows.</item>
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
        await EnsureCapstoneStudentUsersAsync();
        await SeedDemoShowcaseProgramsAsync();
        await SeedReviewDraftProgramsAsync();
        await SeedExpertAdvisoryDemoAsync();
        await SeedAdvisoryChatDemoAsync();
        await EnsureClassSessionCoverageAsync();
        await RealignSeedSessionWallClocksAsync();
        await SeedWeeklyScheduleFixtureAsync();
        await EnsureSeedSessionVenuesAsync();
        // After realign / weekly grid / venues so the LiveOnline + Offline pair stays on the seed clock.
        await ApplyCapstoneLiveSessionsAsync();
        // Catalog, demo, review, and advisory programs exist. Fail/rebuy is linked again below.
        await SeedProgramSkillsAsync();
        await SeedPortfolioDataAsync();
        // Demo clear must run before the capstone class journey (quiz grades would be wiped).
        await ClearDemoProgramSubmissionsAsync();
        await SeedFailRebuyFixturesAsync();
        // Second pass picks up PRG-FAILREBUY. Existing pairs are skipped.
        await SeedProgramSkillsAsync();
        // After FailRebuy so newly created self-paced activities get materials.
        await SeedMaterialsAsync();
        // Safety-net before windows so leftover-fail cannot AcademicFail mid-seed.
        await SeedTaughtModuleAssessmentSafetyNetAsync();
        await EnsureAssignmentWorkWindowsAsync();
        // Demo flow classes: no time-locked quiz or milestone.
        await OpenCapstoneAssignmentWindowsAsync();
        await SeedPassedSubmissionsForElapsedRequiredWindowsAsync();
        await AlignInProgressCurriculumToClassTimetableAsync();
        await SeedCertTestProgressAsync();
        // Before certificates/payments so reviewer Completed enrollments get both.
        await SeedProgramReviewsAsync();
        await SeedCompletedProgramCertificatesAsync();
        await SeedPaymentsAsync();
        await SeedNotificationsAsync();
        await RestoreInProgressPurchasesClosedDuringSeedAsync();
        // Restore-repair only: remove holds on not-yet-open windows and cover reopened seats.
        await SeedTaughtModuleAssessmentSafetyNetAsync();
        await SeedPassedSubmissionsForElapsedRequiredWindowsAsync();
        // After final elapsed-related state: research FileUpload fixtures need ResearchMilestoneId.
        await SeedGradedCapstoneSubmissionForUiAsync();
        // After ClearDemoProgramSubmissionsAsync so Module 1 quiz grades are not wiped.
        await ApplyCapstoneClassJourneyAsync();
        await AlignSeedAttendanceWithDoneSessionActivitiesAsync();
        await SeedClassSessionExpertsAsync();
        // Last, so every account created by earlier steps (reviewers, fail/rebuy, experts) is covered.
        await SeedUserAvatarsAsync();
        await VerifySeedDemoIntegrityAsync();
    }

    public async Task ClearAllDataAsync()
    {
        _loggerService.LogInformation("Starting clear all data");

        // Face vectors live in Rekognition, not PostgreSQL. Indexing is blocked
        // until the collection has been purged again after the truncate.
        await _faceRecognitionService.ResetCollectionAfterAsync(async () =>
        {
            await ClearS3ObjectsAsync();
            await _unitOfWork.TruncateAllApplicationTablesAsync();
            await VerifyDatabaseIsEmptyAsync();
        });

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
        await EnsureTableEmptyAsync(_unitOfWork.FaceEmbeddings, "FaceEmbeddings");
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
