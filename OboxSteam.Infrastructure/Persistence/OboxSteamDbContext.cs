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

    // ── 6. Assessments & Submissions ──
    public DbSet<Assignment> Assignments { get; set; }
    public DbSet<QuestionBank> QuestionBanks { get; set; }
    public DbSet<BankQuestion> BankQuestions { get; set; }
    public DbSet<BankQuestionOption> BankQuestionOptions { get; set; }
    public DbSet<QuizQuestion> QuizQuestions { get; set; }
    public DbSet<QuizOption> QuizOptions { get; set; }
    public DbSet<Submission> Submissions { get; set; }
    public DbSet<SubmissionEvidence> SubmissionEvidences { get; set; }

    // ── 7. Certificates & Portfolio ──
    public DbSet<Certificate> Certificates { get; set; }
    public DbSet<Portfolio> Portfolios { get; set; }
    public DbSet<PortfolioCustomItem> PortfolioCustomItems { get; set; }

    // ── 8. AI Engine & Media ──
    public DbSet<FaceEmbedding> FaceEmbeddings { get; set; }
    public DbSet<MediaAsset> MediaAssets { get; set; }
    public DbSet<MediaTag> MediaTags { get; set; }
    public DbSet<HighlightVideo> HighlightVideos { get; set; }

    // ── 9. Payments ──
    public DbSet<Payment> Payments { get; set; }

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
        modelBuilder.Entity<Assignment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<QuestionBank>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BankQuestion>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BankQuestionOption>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<QuizQuestion>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<QuizOption>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Submission>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Certificate>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Portfolio>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PortfolioCustomItem>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FaceEmbedding>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<MediaAsset>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<HighlightVideo>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Payment>().HasQueryFilter(e => !e.IsDeleted);
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
        // PAYMENT (Unique code)
        // =============================================
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(p => p.Code).IsUnique();
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
    }
}
