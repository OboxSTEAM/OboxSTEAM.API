using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Test.Helpers;

/// <summary>
/// Shared in-memory <see cref="IUnitOfWork"/> for Application unit tests.
/// Exposes typed <see cref="InMemoryRepository{TEntity}"/> for seeding/assertions;
/// service code receives this instance as <see cref="IUnitOfWork"/>.
/// </summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public InMemoryRepository<User> Users { get; } = new();
    public InMemoryRepository<OtpStorage> OtpStorages { get; } = new();
    public InMemoryRepository<Program> Programs { get; } = new();
    public InMemoryRepository<ProgramEnrollment> ProgramEnrollments { get; } = new();
    public InMemoryRepository<Course> Courses { get; } = new();
    public InMemoryRepository<CourseEnrollment> CourseEnrollments { get; } = new();
    public InMemoryRepository<Module> Modules { get; } = new();
    public InMemoryRepository<ModuleEnrollment> ModuleEnrollments { get; } = new();
    public InMemoryRepository<Material> Materials { get; } = new();
    public InMemoryRepository<Assignment> Assignments { get; } = new();
    public InMemoryRepository<Submission> Submissions { get; } = new();
    public InMemoryRepository<QuizQuestion> QuizQuestions { get; } = new();
    public InMemoryRepository<QuizOption> QuizOptions { get; } = new();
    public InMemoryRepository<Activity> Activities { get; } = new();
    public InMemoryRepository<ActivityBooking> ActivityBookings { get; } = new();
    public InMemoryRepository<ActivityProgress> ActivityProgresses { get; } = new();
    public InMemoryRepository<Class> Classes { get; } = new();
    public InMemoryRepository<ClassEnrollment> ClassEnrollments { get; } = new();
    public InMemoryRepository<ClassSession> ClassSessions { get; } = new();
    public InMemoryRepository<SessionAttendance> SessionAttendances { get; } = new();
    public InMemoryRepository<ClassSkill> ClassSkills { get; } = new();
    public InMemoryRepository<ClassMentorRequest> ClassMentorRequests { get; } = new();
    public InMemoryRepository<ClassQuizQuestionSet> ClassQuizQuestionSets { get; } = new();
    public InMemoryRepository<ClassQuizQuestion> ClassQuizQuestions { get; } = new();
    public InMemoryRepository<ClassQuizQuestionOption> ClassQuizQuestionOptions { get; } = new();
    public InMemoryRepository<Expert> Experts { get; } = new();
    public InMemoryRepository<Certificate> Certificates { get; } = new();
    public InMemoryRepository<MediaAsset> MediaAssets { get; } = new();
    public InMemoryRepository<MediaTag> MediaTags { get; } = new();
    public InMemoryRepository<HighlightVideoStack> HighlightVideoStacks { get; } = new();
    public InMemoryRepository<HighlightVideoItem> HighlightVideoItems { get; } = new();
    public InMemoryRepository<FaceEmbedding> FaceEmbeddings { get; } = new();
    public InMemoryRepository<Portfolio> Portfolios { get; } = new();
    public InMemoryRepository<PortfolioCustomItem> PortfolioCustomItems { get; } = new();
    public InMemoryRepository<PortfolioItemSubmission> PortfolioItemSubmissions { get; } = new();
    public InMemoryRepository<PortfolioSection> PortfolioSections { get; } = new();
    public InMemoryRepository<PortfolioMediaAsset> PortfolioMediaAssets { get; } = new();
    public InMemoryRepository<PortfolioMediaPlacement> PortfolioMediaPlacements { get; } = new();
    public InMemoryRepository<ResearchMilestone> ResearchMilestones { get; } = new();
    public InMemoryRepository<ResearchMilestoneActivity> ResearchMilestoneActivities { get; } = new();
    public InMemoryRepository<Skill> Skills { get; } = new();
    public InMemoryRepository<StudentSkill> StudentSkills { get; } = new();
    public InMemoryRepository<StudentSkillEvidence> StudentSkillEvidences { get; } = new();
    public InMemoryRepository<MentorSkill> MentorSkills { get; } = new();
    public InMemoryRepository<StandardizedTest> StandardizedTests { get; } = new();
    public InMemoryRepository<StudentProfile> StudentProfiles { get; } = new();
    public InMemoryRepository<MentorProfile> MentorProfiles { get; } = new();
    public InMemoryRepository<SubmissionEvidence> SubmissionEvidences { get; } = new();
    public InMemoryRepository<Payment> Payments { get; } = new();
    public InMemoryRepository<ProgramBoard> ProgramBoards { get; } = new();
    public InMemoryRepository<ParentStudent> ParentStudents { get; } = new();
    public InMemoryRepository<QuestionBank> QuestionBanks { get; } = new();
    public InMemoryRepository<BankQuestion> BankQuestions { get; } = new();
    public InMemoryRepository<BankQuestionOption> BankQuestionOptions { get; } = new();
    public InMemoryRepository<QuizAnswer> QuizAnswers { get; } = new();
    public InMemoryRepository<ProgramReview> ProgramReviews { get; } = new();
    public InMemoryRepository<PaymentRequest> PaymentRequests { get; } = new();
    public InMemoryRepository<Invoice> Invoices { get; } = new();
    public InMemoryRepository<Notification> Notifications { get; } = new();

    IGenericRepository<User> IUnitOfWork.Users => Users;
    IGenericRepository<OtpStorage> IUnitOfWork.OtpStorages => OtpStorages;
    IGenericRepository<Program> IUnitOfWork.Programs => Programs;
    IGenericRepository<ProgramEnrollment> IUnitOfWork.ProgramEnrollments => ProgramEnrollments;
    IGenericRepository<Course> IUnitOfWork.Courses => Courses;
    IGenericRepository<CourseEnrollment> IUnitOfWork.CourseEnrollments => CourseEnrollments;
    IGenericRepository<Module> IUnitOfWork.Modules => Modules;
    IGenericRepository<ModuleEnrollment> IUnitOfWork.ModuleEnrollments => ModuleEnrollments;
    IGenericRepository<Material> IUnitOfWork.Materials => Materials;
    IGenericRepository<Assignment> IUnitOfWork.Assignments => Assignments;
    IGenericRepository<Submission> IUnitOfWork.Submissions => Submissions;
    IGenericRepository<QuizQuestion> IUnitOfWork.QuizQuestions => QuizQuestions;
    IGenericRepository<QuizOption> IUnitOfWork.QuizOptions => QuizOptions;
    IGenericRepository<Activity> IUnitOfWork.Activities => Activities;
    IGenericRepository<ActivityBooking> IUnitOfWork.ActivityBookings => ActivityBookings;
    IGenericRepository<ActivityProgress> IUnitOfWork.ActivityProgresses => ActivityProgresses;
    IGenericRepository<Class> IUnitOfWork.Classes => Classes;
    IGenericRepository<ClassEnrollment> IUnitOfWork.ClassEnrollments => ClassEnrollments;
    IGenericRepository<ClassSession> IUnitOfWork.ClassSessions => ClassSessions;
    IGenericRepository<SessionAttendance> IUnitOfWork.SessionAttendances => SessionAttendances;
    IGenericRepository<ClassSkill> IUnitOfWork.ClassSkills => ClassSkills;
    IGenericRepository<ClassMentorRequest> IUnitOfWork.ClassMentorRequests => ClassMentorRequests;
    IGenericRepository<ClassQuizQuestionSet> IUnitOfWork.ClassQuizQuestionSets => ClassQuizQuestionSets;
    IGenericRepository<ClassQuizQuestion> IUnitOfWork.ClassQuizQuestions => ClassQuizQuestions;
    IGenericRepository<ClassQuizQuestionOption> IUnitOfWork.ClassQuizQuestionOptions => ClassQuizQuestionOptions;
    IGenericRepository<Expert> IUnitOfWork.Experts => Experts;
    IGenericRepository<Certificate> IUnitOfWork.Certificates => Certificates;
    IGenericRepository<MediaAsset> IUnitOfWork.MediaAssets => MediaAssets;
    IGenericRepository<MediaTag> IUnitOfWork.MediaTags => MediaTags;
    IGenericRepository<HighlightVideoStack> IUnitOfWork.HighlightVideoStacks => HighlightVideoStacks;
    IGenericRepository<HighlightVideoItem> IUnitOfWork.HighlightVideoItems => HighlightVideoItems;
    IGenericRepository<FaceEmbedding> IUnitOfWork.FaceEmbeddings => FaceEmbeddings;
    IGenericRepository<Portfolio> IUnitOfWork.Portfolios => Portfolios;
    IGenericRepository<PortfolioCustomItem> IUnitOfWork.PortfolioCustomItems => PortfolioCustomItems;
    IGenericRepository<PortfolioItemSubmission> IUnitOfWork.PortfolioItemSubmissions => PortfolioItemSubmissions;
    IGenericRepository<PortfolioSection> IUnitOfWork.PortfolioSections => PortfolioSections;
    IGenericRepository<PortfolioMediaAsset> IUnitOfWork.PortfolioMediaAssets => PortfolioMediaAssets;
    IGenericRepository<PortfolioMediaPlacement> IUnitOfWork.PortfolioMediaPlacements => PortfolioMediaPlacements;
    IGenericRepository<ResearchMilestone> IUnitOfWork.ResearchMilestones => ResearchMilestones;
    IGenericRepository<ResearchMilestoneActivity> IUnitOfWork.ResearchMilestoneActivities => ResearchMilestoneActivities;
    IGenericRepository<Skill> IUnitOfWork.Skills => Skills;
    IGenericRepository<StudentSkill> IUnitOfWork.StudentSkills => StudentSkills;
    IGenericRepository<StudentSkillEvidence> IUnitOfWork.StudentSkillEvidences => StudentSkillEvidences;
    IGenericRepository<MentorSkill> IUnitOfWork.MentorSkills => MentorSkills;
    IGenericRepository<StandardizedTest> IUnitOfWork.StandardizedTests => StandardizedTests;
    IGenericRepository<StudentProfile> IUnitOfWork.StudentProfiles => StudentProfiles;
    IGenericRepository<MentorProfile> IUnitOfWork.MentorProfiles => MentorProfiles;
    IGenericRepository<SubmissionEvidence> IUnitOfWork.SubmissionEvidences => SubmissionEvidences;
    IGenericRepository<Payment> IUnitOfWork.Payments => Payments;
    IGenericRepository<ProgramBoard> IUnitOfWork.ProgramBoards => ProgramBoards;
    IGenericRepository<ParentStudent> IUnitOfWork.ParentStudents => ParentStudents;
    IGenericRepository<QuestionBank> IUnitOfWork.QuestionBanks => QuestionBanks;
    IGenericRepository<BankQuestion> IUnitOfWork.BankQuestions => BankQuestions;
    IGenericRepository<BankQuestionOption> IUnitOfWork.BankQuestionOptions => BankQuestionOptions;
    IGenericRepository<QuizAnswer> IUnitOfWork.QuizAnswers => QuizAnswers;
    IGenericRepository<ProgramReview> IUnitOfWork.ProgramReviews => ProgramReviews;
    IGenericRepository<PaymentRequest> IUnitOfWork.PaymentRequests => PaymentRequests;
    IGenericRepository<Invoice> IUnitOfWork.Invoices => Invoices;
    IGenericRepository<Notification> IUnitOfWork.Notifications => Notifications;

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
        var repo = typeof(TEntity).Name switch
        {
            nameof(User) => (object)Users,
            nameof(OtpStorage) => OtpStorages,
            nameof(Program) => Programs,
            nameof(ProgramEnrollment) => ProgramEnrollments,
            nameof(Course) => Courses,
            nameof(CourseEnrollment) => CourseEnrollments,
            nameof(Module) => Modules,
            nameof(ModuleEnrollment) => ModuleEnrollments,
            nameof(Material) => Materials,
            nameof(Assignment) => Assignments,
            nameof(Submission) => Submissions,
            nameof(QuizQuestion) => QuizQuestions,
            nameof(QuizOption) => QuizOptions,
            nameof(Activity) => Activities,
            nameof(ActivityBooking) => ActivityBookings,
            nameof(ActivityProgress) => ActivityProgresses,
            nameof(Class) => Classes,
            nameof(ClassEnrollment) => ClassEnrollments,
            nameof(ClassSession) => ClassSessions,
            nameof(SessionAttendance) => SessionAttendances,
            nameof(ClassSkill) => ClassSkills,
            nameof(ClassMentorRequest) => ClassMentorRequests,
            nameof(ClassQuizQuestionSet) => ClassQuizQuestionSets,
            nameof(ClassQuizQuestion) => ClassQuizQuestions,
            nameof(ClassQuizQuestionOption) => ClassQuizQuestionOptions,
            nameof(Expert) => Experts,
            nameof(Certificate) => Certificates,
            nameof(MediaAsset) => MediaAssets,
            nameof(MediaTag) => MediaTags,
            nameof(HighlightVideoStack) => HighlightVideoStacks,
            nameof(HighlightVideoItem) => HighlightVideoItems,
            nameof(FaceEmbedding) => FaceEmbeddings,
            nameof(Portfolio) => Portfolios,
            nameof(PortfolioCustomItem) => PortfolioCustomItems,
            nameof(PortfolioItemSubmission) => PortfolioItemSubmissions,
            nameof(PortfolioSection) => PortfolioSections,
            nameof(PortfolioMediaAsset) => PortfolioMediaAssets,
            nameof(PortfolioMediaPlacement) => PortfolioMediaPlacements,
            nameof(ResearchMilestone) => ResearchMilestones,
            nameof(ResearchMilestoneActivity) => ResearchMilestoneActivities,
            nameof(Skill) => Skills,
            nameof(StudentSkill) => StudentSkills,
            nameof(StudentSkillEvidence) => StudentSkillEvidences,
            nameof(MentorSkill) => MentorSkills,
            nameof(StandardizedTest) => StandardizedTests,
            nameof(StudentProfile) => StudentProfiles,
            nameof(MentorProfile) => MentorProfiles,
            nameof(SubmissionEvidence) => SubmissionEvidences,
            nameof(Payment) => Payments,
            nameof(ProgramBoard) => ProgramBoards,
            nameof(ParentStudent) => ParentStudents,
            nameof(QuestionBank) => QuestionBanks,
            nameof(BankQuestion) => BankQuestions,
            nameof(BankQuestionOption) => BankQuestionOptions,
            nameof(QuizAnswer) => QuizAnswers,
            nameof(ProgramReview) => ProgramReviews,
            nameof(PaymentRequest) => PaymentRequests,
            nameof(Invoice) => Invoices,
            nameof(Notification) => Notifications,
            _ => throw new NotSupportedException($"No in-memory repository registered for {typeof(TEntity).Name}.")
        };

        return (IGenericRepository<TEntity>)repo;
    }

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }

    public Task TruncateAllApplicationTablesAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }
}
