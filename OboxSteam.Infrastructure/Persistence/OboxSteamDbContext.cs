using Microsoft.EntityFrameworkCore;
using OboxSteam.Domain.Entities;
using OboxSteam.Infrastructure.Commons;

namespace OboxSteam.Infrastructure.Persistence;

public class OboxSteamDbContext : DbContext
{
    public OboxSteamDbContext(DbContextOptions<OboxSteamDbContext> options) : base(options)
    {
    }

    // ── 1. Core Users & Roles ──
    public DbSet<User> Users { get; set; }
    public DbSet<OtpStorage> OtpStorages { get; set; }
    public DbSet<ParentStudent> ParentStudents { get; set; }

    // ── 2. VIP Experts & PR Board ──
    public DbSet<Expert> Experts { get; set; }

    // ── 3. Student Academic Profile ──
    public DbSet<StudentProfile> StudentProfiles { get; set; }
    public DbSet<StudentSkill> StudentSkills { get; set; }
    public DbSet<StandardizedTest> StandardizedTests { get; set; }

    // ── 4. LMS Hierarchy ──
    public DbSet<Program> Programs { get; set; }
    public DbSet<ProgramBoard> ProgramBoards { get; set; }
    public DbSet<Module> Modules { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<Material> Materials { get; set; }

    // ── 5. Enrollments & Tracking ──
    public DbSet<ProgramEnrollment> ProgramEnrollments { get; set; }
    public DbSet<ModuleEnrollment> ModuleEnrollments { get; set; }
    public DbSet<CourseEnrollment> CourseEnrollments { get; set; }
    public DbSet<ActivityBooking> ActivityBookings { get; set; }
    public DbSet<ActivityProgress> ActivityProgresses { get; set; }

    // ── 5b. Class Delivery (cohort scheduling) ──
    public DbSet<Class> Classes { get; set; }
    public DbSet<ClassEnrollment> ClassEnrollments { get; set; }
    public DbSet<ClassSession> ClassSessions { get; set; }
    public DbSet<SessionAttendance> SessionAttendances { get; set; }

    // ── 6. Assessments & Submissions ──
    public DbSet<Assignment> Assignments { get; set; }
    public DbSet<QuestionBank> QuestionBanks { get; set; }
    public DbSet<BankQuestion> BankQuestions { get; set; }
    public DbSet<BankQuestionOption> BankQuestionOptions { get; set; }
    public DbSet<QuizQuestion> QuizQuestions { get; set; }
    public DbSet<QuizOption> QuizOptions { get; set; }
    public DbSet<QuizAnswer> QuizAnswers { get; set; }
    public DbSet<Submission> Submissions { get; set; }
    public DbSet<SubmissionEvidence> SubmissionEvidences { get; set; }

    // ── 7. Certificates & Portfolio ──
    public DbSet<Certificate> Certificates { get; set; }
    public DbSet<Portfolio> Portfolios { get; set; }
    public DbSet<PortfolioCustomItem> PortfolioCustomItems { get; set; }
    public DbSet<PortfolioItemSubmission> PortfolioItemSubmissions { get; set; }

    // ── 7b. Research Milestones ──
    public DbSet<ResearchMilestone> ResearchMilestones { get; set; }
    public DbSet<ResearchMilestoneActivity> ResearchMilestoneActivities { get; set; }

    // ── 8. AI Engine & Media ──
    public DbSet<FaceEmbedding> FaceEmbeddings { get; set; }
    public DbSet<MediaAsset> MediaAssets { get; set; }
    public DbSet<MediaTag> MediaTags { get; set; }
    public DbSet<HighlightVideo> HighlightVideos { get; set; }

    // ── 9. Payments ──
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PaymentRequest> PaymentRequests { get; set; }
    public DbSet<Invoice> Invoices { get; set; }

    // ── 10. Reviews ──
    public DbSet<ProgramReview> ProgramReviews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Store all enum properties as strings in the database instead of integers.
        // This makes the data human-readable and prevents silent bugs when enum values are reordered.
        modelBuilder.UseStringForEnums();

        // =============================================
        // GLOBAL QUERY FILTERS (Soft Delete)
        // =============================================
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<OtpStorage>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Expert>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StudentSkill>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StandardizedTest>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Program>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Module>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Course>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Activity>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Material>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ProgramEnrollment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ModuleEnrollment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CourseEnrollment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ActivityBooking>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ActivityProgress>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Class>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ClassEnrollment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ClassSession>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SessionAttendance>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Assignment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<QuestionBank>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BankQuestion>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BankQuestionOption>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<QuizQuestion>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<QuizOption>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<QuizAnswer>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Submission>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Certificate>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Portfolio>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PortfolioCustomItem>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PortfolioItemSubmission>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ResearchMilestone>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ResearchMilestoneActivity>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FaceEmbedding>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<MediaAsset>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<HighlightVideo>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Payment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PaymentRequest>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Invoice>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ProgramReview>().HasQueryFilter(e => !e.IsDeleted);

        // =============================================
        // 1. USER
        // =============================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // =============================================
        // PARENT-STUDENT (Composite Key Join Table)
        // =============================================
        modelBuilder.Entity<ParentStudent>(entity =>
        {
            entity.HasKey(ps => new { ps.ParentId, ps.StudentId });

            entity.HasOne(ps => ps.Parent)
                .WithMany(u => u.ParentRelations)
                .HasForeignKey(ps => ps.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ps => ps.Student)
                .WithMany(u => u.StudentRelations)
                .HasForeignKey(ps => ps.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =============================================
        // 2. EXPERT (1:1 optional with User)
        // =============================================
        modelBuilder.Entity<Expert>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();

            entity.HasOne(e => e.User)
                .WithOne(u => u.Expert)
                .HasForeignKey<Expert>(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =============================================
        // 3. STUDENT PROFILE (1:1 with User, shared PK)
        // =============================================
        modelBuilder.Entity<StudentProfile>(entity =>
        {
            entity.HasKey(sp => sp.StudentId);

            entity.HasOne(sp => sp.Student)
                .WithOne(u => u.StudentProfile)
                .HasForeignKey<StudentProfile>(sp => sp.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =============================================
        // 4. PROGRAM BOARD (Unique: program + expert)
        // =============================================
        modelBuilder.Entity<ProgramBoard>(entity =>
        {
            entity.HasIndex(pb => new { pb.ProgramId, pb.ExpertId }).IsUnique();
        });

        // =============================================
        // MODULE (Self-referencing prerequisite)
        // =============================================
        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasIndex(m => m.Code).IsUnique();

            entity.Property(m => m.LearningOutcomes)
                .HasColumnType("text[]")
                .HasDefaultValueSql("ARRAY[]::text[]");

            entity.HasOne(m => m.PrerequisiteModule)
                .WithMany()
                .HasForeignKey(m => m.PrerequisiteModuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =============================================
        // COURSE
        // =============================================
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasIndex(c => c.Code).IsUnique();

            entity.HasOne(c => c.Mentor)
                .WithMany()
                .HasForeignKey(c => c.MentorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =============================================
        // ACTIVITY
        // =============================================
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasIndex(a => a.Code).IsUnique();
        });

        // =============================================
        // MATERIAL (1:1 with SelfPaced activity)
        // =============================================
        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasIndex(m => m.ActivityId).IsUnique();

            entity.HasOne(m => m.Activity)
                .WithOne(a => a.Material)
                .HasForeignKey<Material>(m => m.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =============================================
        // ACTIVITY BOOKING (Unique: student + activity)
        // =============================================
        modelBuilder.Entity<ActivityBooking>(entity =>
        {
            entity.HasIndex(ab => new { ab.StudentId, ab.ActivityId }).IsUnique();
        });

        // =============================================
        // PROGRAM ENROLLMENT (Unique: student + program, active only)
        // =============================================
        modelBuilder.Entity<ProgramEnrollment>(entity =>
        {
            entity.HasIndex(pe => new { pe.StudentId, pe.ProgramId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.HasIndex(pe => new { pe.Status, pe.CreatedAt })
                .HasFilter("\"IsDeleted\" = false");
        });

        // =============================================
        // MODULE ENROLLMENT (Unique: student + module + attempt)
        // =============================================
        modelBuilder.Entity<ModuleEnrollment>(entity =>
        {
            entity.HasOne(me => me.ProgramEnrollment)
                .WithMany(pe => pe.ModuleEnrollments)
                .HasForeignKey(me => me.ProgramEnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(me => me.Student)
                .WithMany(u => u.ModuleEnrollments)
                .HasForeignKey(me => me.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(me => new { me.StudentId, me.ModuleId, me.AttemptNumber })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
        });

        // =============================================
        // ACTIVITY PROGRESS (Unique: module enrollment + activity)
        // =============================================
        modelBuilder.Entity<ActivityProgress>(entity =>
        {
            entity.HasOne(ap => ap.ModuleEnrollment)
                .WithMany(me => me.ActivityProgresses)
                .HasForeignKey(ap => ap.ModuleEnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ap => ap.Student)
                .WithMany(u => u.ActivityProgresses)
                .HasForeignKey(ap => ap.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ap => ap.Activity)
                .WithMany(a => a.ActivityProgresses)
                .HasForeignKey(ap => ap.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(ap => new { ap.ModuleEnrollmentId, ap.ActivityId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
        });

        // =============================================
        // ASSIGNMENT
        // =============================================
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasIndex(a => a.Code).IsUnique();

            entity.HasOne(a => a.QuestionBank)
                .WithMany(qb => qb.Assignments)
                .HasForeignKey(a => a.QuestionBankId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =============================================
        // QUESTION BANK
        // =============================================
        modelBuilder.Entity<QuestionBank>(entity =>
        {
            entity.HasIndex(qb => new { qb.CourseId, qb.Name })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.HasOne(qb => qb.Course)
                .WithMany(c => c.QuestionBanks)
                .HasForeignKey(qb => qb.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =============================================
        // BANK QUESTION
        // =============================================
        modelBuilder.Entity<BankQuestion>(entity =>
        {
            entity.HasOne(bq => bq.QuestionBank)
                .WithMany(qb => qb.Questions)
                .HasForeignKey(bq => bq.QuestionBankId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =============================================
        // BANK QUESTION OPTION
        // =============================================
        modelBuilder.Entity<BankQuestionOption>(entity =>
        {
            entity.HasOne(o => o.BankQuestion)
                .WithMany(bq => bq.Options)
                .HasForeignKey(o => o.BankQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =============================================
        // QUIZ QUESTION (snapshot)
        // =============================================
        modelBuilder.Entity<QuizQuestion>(entity =>
        {
            entity.HasOne(qq => qq.BankQuestion)
                .WithMany()
                .HasForeignKey(qq => qq.BankQuestionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(qq => qq.Submission)
                .WithMany(s => s.QuizQuestions)
                .HasForeignKey(qq => qq.SubmissionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =============================================
        // QUIZ ANSWER (student selections)
        // =============================================
        modelBuilder.Entity<QuizAnswer>(entity =>
        {
            entity.HasIndex(qa => new { qa.SubmissionId, qa.QuizQuestionId, qa.QuizOptionId })
                .IsUnique();

            entity.HasOne(qa => qa.Submission)
                .WithMany(s => s.QuizAnswers)
                .HasForeignKey(qa => qa.SubmissionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(qa => qa.QuizQuestion)
                .WithMany(qq => qq.Answers)
                .HasForeignKey(qa => qa.QuizQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(qa => qa.QuizOption)
                .WithMany()
                .HasForeignKey(qa => qa.QuizOptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =============================================
        // SUBMISSION
        // =============================================
        modelBuilder.Entity<Submission>(entity =>
        {
            entity.HasIndex(s => s.Code).IsUnique();

            entity.HasOne(s => s.Verifier)
                .WithMany(u => u.VerifiedSubmissions)
                .HasForeignKey(s => s.VerifiedBy)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(s => s.Student)
                .WithMany(u => u.Submissions)
                .HasForeignKey(s => s.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.ModuleEnrollment)
                .WithMany(me => me.Submissions)
                .HasForeignKey(s => s.ModuleEnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.ResearchMilestone)
                .WithMany(rm => rm.Submissions)
                .HasForeignKey(s => s.ResearchMilestoneId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =============================================
        // SUBMISSION EVIDENCE (Composite Key Join Table)
        // =============================================
        modelBuilder.Entity<SubmissionEvidence>(entity =>
        {
            entity.HasKey(se => new { se.SubmissionId, se.MediaId });
        });

        // =============================================
        // CERTIFICATE
        // =============================================
        modelBuilder.Entity<Certificate>(entity =>
        {
            entity.HasIndex(c => c.Code).IsUnique();
        });

        // =============================================
        // PORTFOLIO (Self-referencing parent, unique subdomain)
        // =============================================
        modelBuilder.Entity<Portfolio>(entity =>
        {
            entity.HasIndex(p => p.Code).IsUnique();
            entity.HasIndex(p => p.Subdomain).IsUnique();

            entity.HasOne(p => p.ParentPortfolio)
                .WithMany()
                .HasForeignKey(p => p.ParentPortfolioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => p.StudentId)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false AND \"ParentPortfolioId\" IS NULL");
        });

        // =============================================
        // PORTFOLIO CUSTOM ITEM
        // =============================================
        modelBuilder.Entity<PortfolioCustomItem>(entity =>
        {
            entity.HasOne(i => i.Program)
                .WithMany()
                .HasForeignKey(i => i.ProgramId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(i => i.ProgramEnrollment)
                .WithMany()
                .HasForeignKey(i => i.ProgramEnrollmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(i => i.Module)
                .WithMany()
                .HasForeignKey(i => i.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(i => i.ModuleEnrollment)
                .WithMany()
                .HasForeignKey(i => i.ModuleEnrollmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(i => i.Submission)
                .WithMany()
                .HasForeignKey(i => i.SubmissionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(i => new { i.ModuleEnrollmentId, i.ItemType })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false AND \"ModuleEnrollmentId\" IS NOT NULL AND \"ItemType\" = 'CapstoneProject'");
        });

        // =============================================
        // PORTFOLIO ITEM SUBMISSION (capstone appendix)
        // =============================================
        modelBuilder.Entity<PortfolioItemSubmission>(entity =>
        {
            entity.HasOne(pis => pis.PortfolioCustomItem)
                .WithMany(i => i.AppendixSubmissions)
                .HasForeignKey(pis => pis.PortfolioCustomItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pis => pis.Submission)
                .WithMany()
                .HasForeignKey(pis => pis.SubmissionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(pis => new { pis.PortfolioCustomItemId, pis.SubmissionId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
        });

        // =============================================
        // RESEARCH MILESTONE
        // =============================================
        modelBuilder.Entity<ResearchMilestone>(entity =>
        {
            entity.HasIndex(rm => rm.Code)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.HasIndex(rm => new { rm.ModuleId, rm.MilestoneOrder })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.HasOne(rm => rm.Module)
                .WithMany(m => m.ResearchMilestones)
                .HasForeignKey(rm => rm.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(rm => rm.Assignment)
                .WithOne(a => a.ResearchMilestone)
                .HasForeignKey<ResearchMilestone>(rm => rm.AssignmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =============================================
        // RESEARCH MILESTONE ACTIVITY
        // =============================================
        modelBuilder.Entity<ResearchMilestoneActivity>(entity =>
        {
            entity.HasOne(rma => rma.ResearchMilestone)
                .WithMany(rm => rm.MilestoneActivities)
                .HasForeignKey(rma => rma.ResearchMilestoneId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rma => rma.Activity)
                .WithMany(a => a.ResearchMilestoneActivities)
                .HasForeignKey(rma => rma.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(rma => new { rma.ResearchMilestoneId, rma.ActivityId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
        });

        // =============================================
        // FACE EMBEDDING (1:1 with User)
        // =============================================
        modelBuilder.Entity<FaceEmbedding>(entity =>
        {
            entity.HasIndex(fe => fe.AwsFaceId).IsUnique();

            entity.HasOne(fe => fe.Student)
                .WithOne(u => u.FaceEmbedding)
                .HasForeignKey<FaceEmbedding>(fe => fe.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =============================================
        // MEDIA TAG (Composite Key Join Table)
        // =============================================
        modelBuilder.Entity<MediaTag>(entity =>
        {
            entity.HasKey(mt => new { mt.MediaId, mt.StudentId });
        });

        // =============================================
        // PAYMENT (Unique code + PaidBy FK)
        // =============================================
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(p => p.Code).IsUnique();

            entity.HasOne(p => p.PaidBy)
                .WithMany(u => u.PaidPayments)
                .HasForeignKey(p => p.PaidById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Student)
                .WithMany(u => u.Payments)
                .HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =============================================
        // PAYMENT REQUEST (token-based parent-pay-for-child)
        // =============================================
        modelBuilder.Entity<PaymentRequest>(entity =>
        {
            entity.HasIndex(pr => pr.Token).IsUnique();

            entity.HasOne(pr => pr.Student)
                .WithMany(u => u.SentPaymentRequests)
                .HasForeignKey(pr => pr.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pr => pr.Parent)
                .WithMany(u => u.ReceivedPaymentRequests)
                .HasForeignKey(pr => pr.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pr => pr.Program)
                .WithMany(p => p.PaymentRequests)
                .HasForeignKey(pr => pr.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pr => pr.ProgramEnrollment)
                .WithMany()
                .HasForeignKey(pr => pr.ProgramEnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pr => pr.Module)
                .WithMany()
                .HasForeignKey(pr => pr.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pr => pr.ModuleEnrollment)
                .WithMany()
                .HasForeignKey(pr => pr.ModuleEnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pr => pr.Payment)
                .WithMany()
                .HasForeignKey(pr => pr.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =============================================
        // CLASS (cohort / đợt học)
        // =============================================
        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasIndex(c => c.Code).IsUnique();

            entity.HasIndex(c => new { c.Status, c.StartDate })
                .HasFilter("\"IsDeleted\" = false");

            entity.HasOne(c => c.Program)
                .WithMany(p => p.Classes)
                .HasForeignKey(c => c.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Mentor)
                .WithMany(u => u.MentoredClasses)
                .HasForeignKey(c => c.MentorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =============================================
        // CLASS ENROLLMENT (Unique: class + student)
        // =============================================
        modelBuilder.Entity<ClassEnrollment>(entity =>
        {
            entity.HasOne(ce => ce.Class)
                .WithMany(c => c.ClassEnrollments)
                .HasForeignKey(ce => ce.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ce => ce.Student)
                .WithMany(u => u.ClassEnrollments)
                .HasForeignKey(ce => ce.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ce => ce.ProgramEnrollment)
                .WithMany(pe => pe.ClassEnrollments)
                .HasForeignKey(ce => ce.ProgramEnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(ce => new { ce.ClassId, ce.StudentId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.HasIndex(ce => new { ce.ClassId, ce.Status })
                .HasFilter("\"IsDeleted\" = false");
        });

        // =============================================
        // CLASS SESSION (cohort calendar event)
        // =============================================
        modelBuilder.Entity<ClassSession>(entity =>
        {
            entity.HasIndex(cs => new { cs.ClassId, cs.StartTime });

            entity.HasOne(cs => cs.Class)
                .WithMany(c => c.ClassSessions)
                .HasForeignKey(cs => cs.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cs => cs.Module)
                .WithMany(m => m.ClassSessions)
                .HasForeignKey(cs => cs.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(cs => cs.Activity)
                .WithMany(a => a.ClassSessions)
                .HasForeignKey(cs => cs.ActivityId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(cs => cs.Assignment)
                .WithMany(a => a.ClassSessions)
                .HasForeignKey(cs => cs.AssignmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =============================================
        // SESSION ATTENDANCE (Unique: session + student)
        // =============================================
        modelBuilder.Entity<SessionAttendance>(entity =>
        {
            entity.HasOne(sa => sa.ClassSession)
                .WithMany(cs => cs.SessionAttendances)
                .HasForeignKey(sa => sa.ClassSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sa => sa.Student)
                .WithMany(u => u.SessionAttendances)
                .HasForeignKey(sa => sa.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sa => sa.ModuleEnrollment)
                .WithMany(me => me.SessionAttendances)
                .HasForeignKey(sa => sa.ModuleEnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sa => sa.Recorder)
                .WithMany(u => u.RecordedSessionAttendances)
                .HasForeignKey(sa => sa.RecordedBy)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(sa => new { sa.ClassSessionId, sa.StudentId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
        });

        // =============================================
        // PROGRAM REVIEW (one review per student per program)
        // =============================================
        modelBuilder.Entity<ProgramReview>(entity =>
        {
            entity.HasIndex(pr => new { pr.ProgramId, pr.StudentId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.HasOne(pr => pr.Program)
                .WithMany(p => p.Reviews)
                .HasForeignKey(pr => pr.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pr => pr.Student)
                .WithMany(u => u.ProgramReviews)
                .HasForeignKey(pr => pr.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =============================================
        // INVOICE (1:1 with Payment, auto-created on success)
        // =============================================
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(i => i.InvoiceNumber).IsUnique();

            // 1:1 with Payment — Payment is the principal
            entity.HasOne(i => i.Payment)
                .WithOne(p => p.Invoice)
                .HasForeignKey<Invoice>(i => i.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            // FK to the user who was billed (parent or student)
            entity.HasOne(i => i.IssuedTo)
                .WithMany(u => u.Invoices)
                .HasForeignKey(i => i.IssuedToId)
                .OnDelete(DeleteBehavior.Restrict);

            // Precision for financial amounts
            entity.Property(i => i.SubTotal).HasPrecision(18, 2);
            entity.Property(i => i.TotalAmount).HasPrecision(18, 2);
        });
    }
}
