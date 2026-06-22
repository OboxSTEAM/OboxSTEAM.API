using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
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

        // ── Phase 1: True leaf tables (no children) ──────────────────────────
        await _unitOfWork.MediaTags.HardRemove(x => true);               // join table
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.QuizAnswers.HardRemove(x => true);             // → Submission, QuizQuestion, QuizOption
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.SubmissionEvidences.HardRemove(x => true);     // → Submission, MediaAsset
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.PortfolioItemSubmissions.HardRemove(x => true); // → PortfolioCustomItem, Submission
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.SessionAttendances.HardRemove(x => true);      // → ClassSession, ModuleEnrollment
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.ActivityProgresses.HardRemove(x => true);      // → ModuleEnrollment, Activity
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.ResearchMilestoneActivities.HardRemove(x => true); // → ResearchMilestone
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.BankQuestionOptions.HardRemove(x => true);     // → BankQuestion
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.QuizOptions.HardRemove(x => true);             // → QuizQuestion
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.QuizQuestions.HardRemove(x => true);           // → Submission (SetNull)
        await _unitOfWork.SaveChangesAsync();

        // ── Phase 2: Mid-leaf tables ──────────────────────────────────────────
        await _unitOfWork.Submissions.HardRemove(x => true);             // → ModuleEnrollment (Restrict)
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.PortfolioCustomItems.HardRemove(x => true);    // → ProgramEnrollment, ModuleEnrollment
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.ActivityBookings.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.ClassEnrollments.HardRemove(x => true);        // → ProgramEnrollment (Restrict)
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.PaymentRequests.HardRemove(x => true);         // → ProgramEnrollment (Restrict)
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Invoices.HardRemove(x => true);                // → Payment (Restrict)
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.ProgramReviews.HardRemove(x => true);          // → Program
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Certificates.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.HighlightVideos.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.FaceEmbeddings.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.MediaAssets.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.StudentSkills.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.StandardizedTests.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.OtpStorages.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.ProgramBoards.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Portfolios.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Payments.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();

        // ── Phase 3: Enrollments & content links ──────────────────────────────
        await _unitOfWork.ModuleEnrollments.HardRemove(x => true);       // → ProgramEnrollment (Restrict)
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CourseEnrollments.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.ProgramEnrollments.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.BankQuestions.HardRemove(x => true);           // → QuestionBank
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.QuestionBanks.HardRemove(x => true);           // → Course
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.ResearchMilestones.HardRemove(x => true);      // → Module, Assignment
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.ClassSessions.HardRemove(x => true);           // → Class, Activity, Assignment
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Materials.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();

        // ── Phase 4: Core LMS entities ────────────────────────────────────────
        await _unitOfWork.Assignments.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Activities.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Classes.HardRemove(x => true);                 // → Program (Restrict)
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Courses.HardRemove(x => true);                 // → Module (implicit)
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Modules.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Programs.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();

        // ── Phase 5: Users ────────────────────────────────────────────────────
        await _unitOfWork.ParentStudents.HardRemove(x => true);          // → User (Restrict) × 2
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.StudentProfiles.HardRemove(x => true);         // → User (Cascade)
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Experts.HardRemove(x => true);                 // → User (SetNull)
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.Users.HardRemove(x => true);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation("Finished clear all data");
    }
}
