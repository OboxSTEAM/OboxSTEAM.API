using Microsoft.Extensions.Logging;
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

    public SeedService(ILogger<SeedService> loggerService, IUnitOfWork unitOfWork, IBlobService blobService)
    {
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
    }

    public async Task SeedAllDataAsync()
    {
        _loggerService.LogInformation("Starting seed all data");

        await SeedUsersAsync();
        await EnsureAdditionalMentorUsersAsync();
        await SeedExpertsAsync();
        await SeedProgramsAsync();
        await SeedProgramBoardsAsync();
        await SeedModulesAsync();
        await SeedCoursesAsync();
        await SeedActivitiesAsync();
        await SeedParentStudentLinksAsync();
        await SeedProgramEnrollmentsAsync();
        await SeedModuleEnrollmentsAsync();
        await SeedCourseEnrollmentsAsync();
        await SeedActivityBookingsAsync();
        await SeedAssignmentsAsync();
        await SeedPaymentsAsync();
        await SeedProgramReviewsAsync();
        await SeedMentorClassesAsync();
        await SeedRoboticsClassSessionsAsync();
        await SeedResearchMilestoneDataAsync();
        await SeedResearchModuleEnrollmentsAsync();
        await ResetIntroductionToRoboticsFeTestProgressAsync();
        await SeedResearchActivityProgressAsync();
        await SeedEnrollmentActivityProgressAsync();
        await BackfillActivityProgressStatusAsync();
        await SeedResearchSubmissionsAsync();
        await SeedExtendedResearchDataAsync();
        await SeedMaterialsAsync();

        _loggerService.LogInformation("Finished seed all data");
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
