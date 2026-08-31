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

        using (SeedExecutionGuard.Begin())
        {
            await SeedAllDataCoreAsync();
        }

        _loggerService.LogInformation("Finished seed all data");
    }

    private async Task SeedAllDataCoreAsync()
    {
        await SeedUsersAsync();
        await EnsureAdditionalMentorUsersAsync();
        await EnsureExpertUsersAsync();
        await SeedMentorProfilesAsync();
        await SeedExpertsAsync();
        await SeedProgramsAsync();
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
        await SeedDemoShowcaseProgramsAsync();
        await SeedMaterialsAsync();
        await EnsureClassSessionCoverageAsync();
        await RealignSeedSessionWallClocksAsync();
        await SeedWeeklyScheduleFixtureAsync();
        await EnsureSeedSessionVenuesAsync();
        await SeedPortfolioDataAsync();
        await ClearDemoProgramSubmissionsAsync();
        await SeedFailRebuyFixturesAsync();
        await SeedMaterialsAsync();
        await SeedTaughtModuleAssessmentSafetyNetAsync();
        await EnsureAssignmentWorkWindowsAsync();
        await SeedPassedSubmissionsForElapsedRequiredWindowsAsync();
        await AlignInProgressCurriculumToClassTimetableAsync();
        await SeedCertTestProgressAsync();
        await SeedGradedCapstoneSubmissionForUiAsync();
        await SeedCompletedProgramCertificatesAsync();
        await SeedPaymentsAsync();
        await SeedProgramReviewsAsync();
        await SeedNotificationsAsync();
        await RestoreInProgressPurchasesClosedDuringSeedAsync();
        await SeedTaughtModuleAssessmentSafetyNetAsync();
        await SeedPassedSubmissionsForElapsedRequiredWindowsAsync();
        await AlignInProgressCurriculumToClassTimetableAsync();
        // After RealignSeedSessionWallClocksAsync so Sat/Sun grid does not overwrite
        // the Maker Slice-2 join/check-in pair used by student10 FE testing.
        await ApplyMakerSlice2JoinableSessionsAsync();
        // After ClearDemoProgramSubmissionsAsync so STD-010 theory quiz grade is not wiped.
        await ApplyMakerStudent10Module1CompleteAsync();
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
        if (await _unitOfWork.Users.AnyIncludingDeletedAsync())
        {
            throw ErrorHelper.Internal("Clear incomplete: Users table still has rows.");
        }

        if (await _unitOfWork.Programs.AnyIncludingDeletedAsync())
        {
            throw ErrorHelper.Internal("Clear incomplete: Programs table still has rows.");
        }

        if (await _unitOfWork.Activities.AnyIncludingDeletedAsync())
        {
            throw ErrorHelper.Internal("Clear incomplete: Activities table still has rows.");
        }

        if (await _unitOfWork.ResearchMilestones.AnyIncludingDeletedAsync())
        {
            throw ErrorHelper.Internal("Clear incomplete: ResearchMilestones table still has rows.");
        }
    }
}
