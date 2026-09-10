using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Slice 7 demo catalog: Robotics pathway plus extra STEAM bundles, OBX15 voucher,
/// mid-path enrollment, and a retail owner for price-quote. Idempotent for re-seed without clear.
/// </summary>
public partial class SeedService
{
    internal const string SeedRoboticsBundleCode = "BDL-ROBOTICS";
    internal const string SeedCodingBundleCode = "BDL-CODING";
    internal const string SeedScienceBundleCode = "BDL-SCIENCE";
    internal const string SeedCreativeBundleCode = "BDL-CREATIVE";
    internal const string SeedMakerBundleCode = "BDL-MAKER";
    internal const string SeedDataAiBundleCode = "BDL-DATA-AI";
    internal const string SeedDraftMathBundleCode = "BDL-DRAFT-MATH";
    internal const string SeedRoboticsIntermediateProgramCode = "PRG-ROBOTICS-INT";
    internal const string SeedRoboticsAdvancedProgramCode = "PRG-ROBOTICS-ADV";
    internal const string SeedVoucherObx15Code = "OBX15";
    internal const string SeedBundleMidPathStudentCode = "STD-011";
    internal const string SeedBundleRetailOwnerStudentCode = "STD-001";

    private const decimal SeedRoboticsIntermediatePrice = 1_500_000m;
    private const decimal SeedRoboticsAdvancedPrice = 1_800_000m;
    private const decimal SeedRoboticsBundlePrice = 3_825_000m;
    private const string SeedRoboticsBundleThumbnail =
        "https://images.unsplash.com/photo-1518314916381-77a37c2a49ae?q=80&w=1171&auto=format&fit=crop";

    /// <summary>
    /// After payments so the mid-path INT enrollment is not billed as a standalone program.
    /// STD-001 already holds Active PRG-ROBOTICS (retail) for GET /api/bundles/{id}/price-quote.
    /// STD-011 completed PRG-ROBOTICS; this seed adds the bundle + unlocked intermediate PE.
    /// </summary>
    private async Task SeedBundlesAndVouchersAsync()
    {
        _loggerService.LogInformation("Starting seed robotics bundle, voucher OBX15, and pathway enrollments");

        var intro = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == "PRG-ROBOTICS" && !p.IsDeleted);
        if (intro == null)
        {
            _loggerService.LogWarning("PRG-ROBOTICS missing. Skipping bundle/voucher seed.");
            return;
        }

        var frameworkId = intro.FrameworkId;
        if (!frameworkId.HasValue)
        {
            var roboticsFramework = await _unitOfWork.ProgramFrameworks.FirstOrDefaultAsync(
                f => f.Name == SeedFrameworkRoboticsName && !f.IsDeleted);
            frameworkId = roboticsFramework?.Id;
        }

        Guid? frameworkVersionId = intro.FrameworkVersionId;
        if (frameworkId.HasValue && !frameworkVersionId.HasValue)
        {
            frameworkVersionId = (await _unitOfWork.ProgramFrameworkVersions.GetAllAsync(
                    v => v.FrameworkId == frameworkId.Value && v.IsPublished && !v.IsDeleted))
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault()?.Id;
        }

        var intermediate = await EnsureRoboticsPathwayProgramAsync(
            SeedRoboticsIntermediateProgramCode,
            "Robotics trung cấp",
            "Cảm biến, điều khiển, và thử thách nhiều robot. Cùng khung Robotics cơ bản.",
            DifficultyLevel.Intermediate,
            SeedRoboticsIntermediatePrice,
            "6 weeks at 3 hours a week",
            "Sensors, control systems, multi-robot challenges",
            frameworkId,
            frameworkVersionId);

        var advanced = await EnsureRoboticsPathwayProgramAsync(
            SeedRoboticsAdvancedProgramCode,
            "Robotics nâng cao",
            "Điều hướng tự hành và robot thi đấu. Cùng khung Robotics cơ bản.",
            DifficultyLevel.Advanced,
            SeedRoboticsAdvancedPrice,
            "8 weeks at 4 hours a week",
            "Autonomous navigation, competition robotics",
            frameworkId,
            frameworkVersionId);

        var bundle = await EnsureRoboticsBundleAsync(intro, intermediate, advanced, frameworkId);
        await EnsureCatalogBundlesAsync();
        await EnsureObx15VoucherAsync();
        await _unitOfWork.SaveChangesAsync();

        await EnsureMidPathBundleEnrollmentAsync(bundle, intro, intermediate);

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed robotics bundle {BundleCode} plus catalog bundles, voucher {VoucherCode}, mid-path {StudentCode}. Retail quote owner is {RetailOwner}.",
            SeedRoboticsBundleCode,
            SeedVoucherObx15Code,
            SeedBundleMidPathStudentCode,
            SeedBundleRetailOwnerStudentCode);
    }

    /// <summary>
    /// Extra STEAM catalog rows for GET /api/bundles. Active bundles are public;
    /// <see cref="SeedDraftMathBundleCode"/> stays Draft for manager-only list checks.
    /// </summary>
    private async Task EnsureCatalogBundlesAsync()
    {
        await EnsureCatalogBundleAsync(
            SeedCodingBundleCode,
            "Lộ trình lập trình: Python đến Game",
            "Python cơ bản, Web Development, rồi Game Design. Giá bundle thấp hơn mua lẻ.",
            ProgramCategory.Technology,
            ProgramBundleStatus.Active,
            ["PRG-PYBASIC", "PRG-WEBDEV", "PRG-GAMEDEV"],
            createdAt: AtDays(-8));

        await EnsureCatalogBundleAsync(
            SeedScienceBundleCode,
            "Lộ trình khoa học sự sống và môi trường",
            "Biotechnology rồi Environmental Science. Hai chương trình Science, giá đã chiết khấu.",
            ProgramCategory.Science,
            ProgramBundleStatus.Active,
            ["PRG-BIOTECH", "PRG-ENVSCI"],
            createdAt: AtDays(-6));

        await EnsureCatalogBundleAsync(
            SeedCreativeBundleCode,
            "Lộ trình sáng tạo: Digital Art và Music",
            "Digital Art & Illustration cùng Music Production. Dành cho học viên Creative Studio.",
            ProgramCategory.Art,
            ProgramBundleStatus.Active,
            ["PRG-DIGART", "PRG-MUSICTECH"],
            createdAt: AtDays(-5));

        await EnsureCatalogBundleAsync(
            SeedMakerBundleCode,
            "Lộ trình maker: IoT và 3D Design",
            "Internet of Things Fundamentals rồi 3D Modeling. Engineering track, giá thấp hơn tổng lẻ.",
            ProgramCategory.Engineering,
            ProgramBundleStatus.Active,
            ["PRG-IOT", "PRG-3DDESIGN"],
            createdAt: AtDays(-4));

        await EnsureCatalogBundleAsync(
            SeedDataAiBundleCode,
            "Lộ trình dữ liệu và AI",
            "AI & Machine Learning for Kids cùng Statistics & Data Analysis.",
            ProgramCategory.Technology,
            ProgramBundleStatus.Active,
            ["PRG-AIBASIC", "PRG-DATAMATH"],
            createdAt: AtDays(-3));

        await EnsureCatalogBundleAsync(
            SeedDraftMathBundleCode,
            "Lộ trình toán (nháp)",
            "Fun with Mathematics và Statistics. Draft — Student/Parent không thấy trên list.",
            ProgramCategory.Mathematic,
            ProgramBundleStatus.Draft,
            ["PRG-MATHFUN", "PRG-DATAMATH"],
            createdAt: AtDays(-1));
    }

    private async Task EnsureCatalogBundleAsync(
        string code,
        string name,
        string description,
        ProgramCategory category,
        ProgramBundleStatus status,
        IReadOnlyList<string> programCodes,
        DateTime createdAt)
    {
        var programs = new List<Program>(programCodes.Count);
        foreach (var programCode in programCodes)
        {
            var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
                p => p.Code == programCode && !p.IsDeleted);
            if (program == null)
            {
                _loggerService.LogWarning(
                    "Program {ProgramCode} missing. Skipping catalog bundle {BundleCode}.",
                    programCode,
                    code);
                return;
            }

            programs.Add(program);
        }

        var retailTotal = programs.Sum(p => p.Price ?? 0m);
        var price = Math.Round(retailTotal * 0.85m, 0, MidpointRounding.AwayFromZero);
        var thumbnail = programs[0].ThumbnailUrl;

        var bundle = await _unitOfWork.ProgramBundles.FirstOrDefaultAsync(
            b => b.Code == code && !b.IsDeleted);
        if (bundle == null)
        {
            bundle = new ProgramBundle
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Description = description,
                ThumbnailUrl = thumbnail,
                Category = category,
                Price = price,
                Status = status,
                CreatedAt = createdAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramBundles.AddAsync(bundle);
        }
        else
        {
            var changed = false;
            if (bundle.Name != name)
            {
                bundle.Name = name;
                changed = true;
            }

            if (bundle.Description != description)
            {
                bundle.Description = description;
                changed = true;
            }

            if (bundle.Category != category)
            {
                bundle.Category = category;
                changed = true;
            }

            if (bundle.Price != price)
            {
                bundle.Price = price;
                changed = true;
            }

            if (bundle.Status != status)
            {
                bundle.Status = status;
                changed = true;
            }

            if (bundle.ThumbnailUrl != thumbnail)
            {
                bundle.ThumbnailUrl = thumbnail;
                changed = true;
            }

            if (changed)
                await _unitOfWork.ProgramBundles.Update(bundle);
        }

        for (var i = 0; i < programs.Count; i++)
        {
            await EnsureBundleItemAsync(
                bundle.Id,
                programs[i].Id,
                sortOrder: i + 1,
                requiresPreviousCompletion: i > 0);
        }
    }

    private async Task<Program> EnsureRoboticsPathwayProgramAsync(
        string code,
        string name,
        string description,
        DifficultyLevel level,
        decimal price,
        string estimatedDuration,
        string skillsGained,
        Guid? frameworkId,
        Guid? frameworkVersionId)
    {
        var existing = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == code && !p.IsDeleted);
        if (existing != null)
        {
            var changed = false;
            if (existing.Status != ProgramStatus.Active)
            {
                existing.Status = ProgramStatus.Active;
                changed = true;
            }

            if (existing.Price != price)
            {
                existing.Price = price;
                changed = true;
            }

            if (frameworkId.HasValue && existing.FrameworkId != frameworkId)
            {
                existing.FrameworkId = frameworkId;
                changed = true;
            }

            if (frameworkVersionId.HasValue && existing.FrameworkVersionId != frameworkVersionId)
            {
                existing.FrameworkVersionId = frameworkVersionId;
                changed = true;
            }

            if (changed)
                await _unitOfWork.Programs.Update(existing);

            return existing;
        }

        var program = new Program
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            SeriesName = "Robotics from beginner to advanced",
            Description = description,
            Level = level,
            Category = ProgramCategory.Technology,
            EstimatedDuration = estimatedDuration,
            SkillsGained = skillsGained,
            Rating = 4.6m,
            TotalReviews = 24,
            ThumbnailUrl = SeedRoboticsBundleThumbnail,
            Status = ProgramStatus.Active,
            Price = price,
            FrameworkId = frameworkId,
            FrameworkVersionId = frameworkVersionId,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Programs.AddAsync(program);
        return program;
    }

    private async Task<ProgramBundle> EnsureRoboticsBundleAsync(
        Program intro,
        Program intermediate,
        Program advanced,
        Guid? frameworkId)
    {
        var bundle = await _unitOfWork.ProgramBundles.FirstOrDefaultAsync(
            b => b.Code == SeedRoboticsBundleCode && !b.IsDeleted);
        if (bundle == null)
        {
            bundle = new ProgramBundle
            {
                Id = Guid.NewGuid(),
                Code = SeedRoboticsBundleCode,
                Name = "Robotics từ cơ bản đến nâng cao",
                Description =
                    "Lộ trình 3 chương trình cùng khung Robotics. Giá bundle thấp hơn tổng lẻ; "
                    + "học viên đã mua Introduction to Robotics vẫn mua được — giá tự trừ.",
                ThumbnailUrl = SeedRoboticsBundleThumbnail,
                Category = ProgramCategory.Technology,
                FrameworkId = frameworkId,
                Price = SeedRoboticsBundlePrice,
                Status = ProgramBundleStatus.Active,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramBundles.AddAsync(bundle);
        }
        else
        {
            var changed = false;
            if (bundle.Status != ProgramBundleStatus.Active)
            {
                bundle.Status = ProgramBundleStatus.Active;
                changed = true;
            }

            if (bundle.Price != SeedRoboticsBundlePrice)
            {
                bundle.Price = SeedRoboticsBundlePrice;
                changed = true;
            }

            if (frameworkId.HasValue && bundle.FrameworkId != frameworkId)
            {
                bundle.FrameworkId = frameworkId;
                changed = true;
            }

            if (changed)
                await _unitOfWork.ProgramBundles.Update(bundle);
        }

        await EnsureBundleItemAsync(bundle.Id, intro.Id, sortOrder: 1, requiresPreviousCompletion: false);
        await EnsureBundleItemAsync(bundle.Id, intermediate.Id, sortOrder: 2, requiresPreviousCompletion: true);
        await EnsureBundleItemAsync(bundle.Id, advanced.Id, sortOrder: 3, requiresPreviousCompletion: true);
        return bundle;
    }

    private async Task EnsureBundleItemAsync(
        Guid bundleId,
        Guid programId,
        int sortOrder,
        bool requiresPreviousCompletion)
    {
        var existing = await _unitOfWork.ProgramBundleItems.FirstOrDefaultAsync(
            i => i.BundleId == bundleId && i.ProgramId == programId && !i.IsDeleted);
        if (existing != null)
        {
            if (existing.SortOrder != sortOrder
                || existing.RequiresPreviousCompletion != requiresPreviousCompletion)
            {
                existing.SortOrder = sortOrder;
                existing.RequiresPreviousCompletion = requiresPreviousCompletion;
                await _unitOfWork.ProgramBundleItems.Update(existing);
            }

            return;
        }

        await _unitOfWork.ProgramBundleItems.AddAsync(new ProgramBundleItem
        {
            Id = Guid.NewGuid(),
            BundleId = bundleId,
            ProgramId = programId,
            SortOrder = sortOrder,
            RequiresPreviousCompletion = requiresPreviousCompletion,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task EnsureObx15VoucherAsync()
    {
        var existing = await _unitOfWork.Vouchers.FirstOrDefaultAsync(
            v => v.Code == SeedVoucherObx15Code && !v.IsDeleted);
        if (existing != null)
        {
            if (existing.Status != VoucherStatus.Active
                || existing.PercentOff != 15m
                || existing.Scope != VoucherScope.Both)
            {
                existing.Status = VoucherStatus.Active;
                existing.PercentOff = 15m;
                existing.AmountOff = null;
                existing.Scope = VoucherScope.Both;
                existing.StartsAt = null;
                await _unitOfWork.Vouchers.Update(existing);
            }

            return;
        }

        await _unitOfWork.Vouchers.AddAsync(new Voucher
        {
            Id = Guid.NewGuid(),
            Code = SeedVoucherObx15Code,
            PercentOff = 15m,
            AmountOff = null,
            StartsAt = null,
            ExpiryAt = AtMonths(12),
            UsageLimit = 100,
            MaxUsagePerStudent = 1,
            Scope = VoucherScope.Both,
            Status = VoucherStatus.Active,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task EnsureMidPathBundleEnrollmentAsync(
        ProgramBundle bundle,
        Program intro,
        Program intermediate)
    {
        var student = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.Code == SeedBundleMidPathStudentCode && !u.IsDeleted);
        if (student == null || student.Role != RoleType.Student)
        {
            _loggerService.LogWarning(
                "{StudentCode} missing. Skipping mid-path bundle enrollment.",
                SeedBundleMidPathStudentCode);
            return;
        }

        var introEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == student.Id
                  && pe.ProgramId == intro.Id
                  && !pe.IsDeleted
                  && pe.Status == EnrollmentStatus.Completed);
        if (introEnrollment == null)
        {
            _loggerService.LogWarning(
                "{StudentCode} has no Completed PRG-ROBOTICS enrollment. Skipping mid-path bundle enrollment.",
                SeedBundleMidPathStudentCode);
            return;
        }

        var purchasedAt = AtDays(-2);
        var intermediateEnrollment = await EnsureActiveProgramEnrollmentAsync(
            student.Id, intermediate.Id, purchasedAt);

        var progressPercent = Math.Round(
            (introEnrollment.ProgressPercent
             + intermediateEnrollment.ProgressPercent
             + 0m) / 3m,
            2);

        var bundleEnrollment = await _unitOfWork.BundleEnrollments.FirstOrDefaultAsync(
            e => e.StudentId == student.Id && e.BundleId == bundle.Id && !e.IsDeleted);
        if (bundleEnrollment == null)
        {
            bundleEnrollment = new BundleEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                BundleId = bundle.Id,
                Status = BundleEnrollmentStatus.Active,
                ProgressPercent = progressPercent,
                CreatedAt = purchasedAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.BundleEnrollments.AddAsync(bundleEnrollment);
        }
        else if (bundleEnrollment.Status != BundleEnrollmentStatus.Active
                 || bundleEnrollment.ProgressPercent != progressPercent)
        {
            bundleEnrollment.Status = BundleEnrollmentStatus.Active;
            bundleEnrollment.ProgressPercent = progressPercent;
            await _unitOfWork.BundleEnrollments.Update(bundleEnrollment);
        }

        var ownershipDeduction = intro.Price ?? 0m;
        var charged = BundlePricingHelper.ClampNonNegative(bundle.Price - ownershipDeduction);
        await EnsureBundlePaymentAsync(
            student,
            bundle,
            bundleEnrollment.Id,
            charged,
            ownershipDeduction,
            purchasedAt);

        _loggerService.LogInformation(
            "Mid-path bundle ready for {StudentCode}: intro Completed, intermediate Active (unlocked), advanced locked.",
            SeedBundleMidPathStudentCode);
    }

    private async Task<ProgramEnrollment> EnsureActiveProgramEnrollmentAsync(
        Guid studentId,
        Guid programId,
        DateTime enrolledAt)
    {
        var existing = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == studentId
                  && pe.ProgramId == programId
                  && !pe.IsDeleted
                  && (pe.Status == EnrollmentStatus.Active || pe.Status == EnrollmentStatus.Completed));
        if (existing != null)
            return existing;

        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ProgramId = programId,
            Status = EnrollmentStatus.Active,
            ProgressPercent = 0m,
            EnrolledAt = enrolledAt,
            CreatedAt = enrolledAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.ProgramEnrollments.AddAsync(enrollment);
        return enrollment;
    }

    private async Task EnsureBundlePaymentAsync(
        User student,
        ProgramBundle bundle,
        Guid bundleEnrollmentId,
        decimal amount,
        decimal discountAmount,
        DateTime purchasedAt)
    {
        var existing = await _unitOfWork.Payments.FirstOrDefaultAsync(
            p => p.BundleEnrollmentId == bundleEnrollmentId && !p.IsDeleted);
        if (existing != null)
            return;

        var paidAt = purchasedAt.AddHours(-1);
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Code = "INV-BDL-011",
            StudentId = student.Id,
            PaidById = student.Id,
            BundleEnrollmentId = bundleEnrollmentId,
            DiscountAmount = discountAmount,
            Amount = amount,
            Gateway = PaymentGateway.Stripe,
            TransactionId = "SEED-TXN-BUNDLE-011",
            Status = PaymentStatus.Success,
            PaidAt = paidAt,
            Currency = "VND",
            CreatedAt = paidAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.Invoices.AddAsync(new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-SEED-BUNDLE-011",
            PaymentId = payment.Id,
            IssuedToId = student.Id,
            BillingName = student.FullName ?? student.Email,
            BillingEmail = student.Email,
            ItemDescription = $"{bundle.Name} tuition",
            SubTotal = bundle.Price,
            TotalAmount = amount,
            Currency = "VND",
            CreatedAt = paidAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }
}
