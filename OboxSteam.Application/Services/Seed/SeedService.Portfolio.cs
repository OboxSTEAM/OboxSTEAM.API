using System.Text.Json;
using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private static readonly JsonSerializerOptions PortfolioSeedJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Seeds STD-001 with a completed PRG-WEBDEV enrollment, certificates,
    /// graded research submissions, and a populated but unpublished portfolio.
    /// </summary>
    private async Task SeedPortfolioDataAsync()
    {
        _loggerService.LogInformation("Starting seed portfolio data for STD-001 (PRG-WEBDEV)");

        var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
        var moduleWebDev1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-01");
        var moduleWebDev2 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-02");
        var moduleWebDev3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-03");

        if (student == null || mentor == null || programWebDev == null || moduleWebDev3 == null)
        {
            _loggerService.LogWarning(
                "Portfolio seed prerequisites missing (STD-001 / MNT-001 / PRG-WEBDEV / MOD-WEBDEV-03). Skipping.");
            return;
        }

        var seedTime = _seedNow;

        var programEnrollment = await EnsureCompletedWebDevProgramEnrollmentAsync(
            student,
            programWebDev,
            seedTime);

        var moduleEnrollments = await EnsureCompletedWebDevModuleEnrollmentsAsync(
            student,
            programEnrollment,
            new[] { moduleWebDev1, moduleWebDev2, moduleWebDev3 },
            seedTime);

        var researchEnrollment = moduleEnrollments[moduleWebDev3.Id];

        var certificates = await SeedPortfolioCertificatesAsync(
            student,
            programWebDev,
            moduleWebDev1,
            seedTime);

        var submissions = await SeedPortfolioResearchSubmissionsAsync(
            student,
            mentor,
            researchEnrollment,
            seedTime);
        if (submissions == null)
        {
            _loggerService.LogWarning(
                "Portfolio research submissions could not be seeded. Skipping portfolio item wiring.");
            return;
        }

        var (wireframeSubmission, capstoneSubmission) = submissions.Value;

        var portfolio = await SeedStudent1PortfolioAsync(student, seedTime);
        await SeedPortfolioItemsAsync(
            portfolio,
            programWebDev,
            moduleWebDev1,
            moduleWebDev3,
            programEnrollment,
            researchEnrollment,
            certificates,
            wireframeSubmission,
            capstoneSubmission,
            seedTime);
        await SeedPortfolioSectionsAsync(portfolio, seedTime);

        _loggerService.LogInformation(
            "Finished seed portfolio data for STD-001 — portfolio {PortfolioCode}, {CertCount} certificate(s).",
            portfolio.Code,
            certificates.Count);
    }

    private async Task<ProgramEnrollment> EnsureCompletedWebDevProgramEnrollmentAsync(
        User student,
        Program programWebDev,
        DateTime seedTime)
    {
        var enrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == student.Id
                  && pe.ProgramId == programWebDev.Id
                  && !pe.IsDeleted);

        if (enrollment == null)
        {
            enrollment = new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ProgramId = programWebDev.Id,
                Status = EnrollmentStatus.Completed,
                ProgressPercent = 100m,
                EnrolledAt = seedTime.AddDays(-90),
                StartedAt = seedTime.AddDays(-85),
                CompletedAt = seedTime.AddDays(-5),
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramEnrollments.AddAsync(enrollment);
            await _unitOfWork.SaveChangesAsync();
            return enrollment;
        }

        if (enrollment.Status != EnrollmentStatus.Completed || enrollment.ProgressPercent != 100m)
        {
            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.ProgressPercent = 100m;
            enrollment.StartedAt ??= seedTime.AddDays(-85);
            enrollment.CompletedAt ??= seedTime.AddDays(-5);
            await _unitOfWork.ProgramEnrollments.Update(enrollment);
            await _unitOfWork.SaveChangesAsync();
        }

        return enrollment;
    }

    private async Task<Dictionary<Guid, ModuleEnrollment>> EnsureCompletedWebDevModuleEnrollmentsAsync(
        User student,
        ProgramEnrollment programEnrollment,
        Module?[] modules,
        DateTime seedTime)
    {
        var result = new Dictionary<Guid, ModuleEnrollment>();
        var completionOffsetDays = -60;

        foreach (var module in modules)
        {
            if (module == null)
            {
                continue;
            }

            var enrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student.Id
                      && me.ModuleId == module.Id
                      && !me.IsDeleted);

            if (enrollment == null)
            {
                enrollment = new ModuleEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    ModuleId = module.Id,
                    ProgramEnrollmentId = programEnrollment.Id,
                    Status = EnrollmentStatus.Completed,
                    ProgressPercent = 100m,
                    FinalGrade = 90m + module.ModuleOrder,
                    EnrolledAt = seedTime.AddDays(completionOffsetDays - 20),
                    StartedAt = seedTime.AddDays(completionOffsetDays - 18),
                    CompletedAt = seedTime.AddDays(completionOffsetDays),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                };
                await _unitOfWork.ModuleEnrollments.AddAsync(enrollment);
                await _unitOfWork.SaveChangesAsync();
            }
            else if (enrollment.Status != EnrollmentStatus.Completed)
            {
                enrollment.Status = EnrollmentStatus.Completed;
                enrollment.ProgressPercent = 100m;
                enrollment.FinalGrade ??= 90m;
                enrollment.CompletedAt ??= seedTime.AddDays(completionOffsetDays);
                await _unitOfWork.ModuleEnrollments.Update(enrollment);
                await _unitOfWork.SaveChangesAsync();
            }

            result[module.Id] = enrollment;
            completionOffsetDays += 20;
        }

        return result;
    }

    private async Task<List<Certificate>> SeedPortfolioCertificatesAsync(
        User student,
        Program programWebDev,
        Module? moduleWebDev1,
        DateTime seedTime)
    {
        var certificates = new List<Certificate>();

        var programCert = await _unitOfWork.Certificates.FirstOrDefaultAsync(
            c => c.Code == "OBOX-CERT-PF-WEBDEV" && !c.IsDeleted);
        if (programCert == null)
        {
            programCert = new Certificate
            {
                Id = Guid.NewGuid(),
                Code = "OBOX-CERT-PF-WEBDEV",
                StudentId = student.Id,
                ProgramId = programWebDev.Id,
                ModuleId = null,
                IssueDate = seedTime.AddDays(-3),
                PdfUrl = "https://storage.oboxsteam.com/certificates/obox-cert-pf-webdev.pdf",
                VerificationUrl = "https://oboxsteam.website/certificates/verify/OBOX-CERT-PF-WEBDEV",
                SkillsAcquired = programWebDev.SkillsGained,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.Certificates.AddAsync(programCert);
            await _unitOfWork.SaveChangesAsync();
        }

        certificates.Add(programCert);

        if (moduleWebDev1 != null)
        {
            var moduleCert = await _unitOfWork.Certificates.FirstOrDefaultAsync(
                c => c.Code == "OBOX-CERT-PF-WD-MOD1" && !c.IsDeleted);
            if (moduleCert == null)
            {
                moduleCert = new Certificate
                {
                    Id = Guid.NewGuid(),
                    Code = "OBOX-CERT-PF-WD-MOD1",
                    StudentId = student.Id,
                    ProgramId = programWebDev.Id,
                    ModuleId = moduleWebDev1.Id,
                    IssueDate = seedTime.AddDays(-55),
                    PdfUrl = "https://storage.oboxsteam.com/certificates/obox-cert-pf-wd-mod1.pdf",
                    VerificationUrl = "https://oboxsteam.website/certificates/verify/OBOX-CERT-PF-WD-MOD1",
                    SkillsAcquired = "Semantic HTML, modern CSS layouts, responsive foundations",
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                };
                await _unitOfWork.Certificates.AddAsync(moduleCert);
                await _unitOfWork.SaveChangesAsync();
            }

            certificates.Add(moduleCert);
        }

        return certificates;
    }

    private async Task<(Submission Wireframe, Submission Capstone)?>
        SeedPortfolioResearchSubmissionsAsync(
            User student,
            User mentor,
            ModuleEnrollment researchEnrollment,
            DateTime seedTime)
    {
        var milestoneWireframe = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-WEBDEV-03-01" && !rm.IsDeleted);
        var milestoneCapstone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code == "RML-WEBDEV-03-02" && !rm.IsDeleted);

        if (milestoneWireframe == null || milestoneCapstone == null)
        {
            _loggerService.LogWarning(
                "WebDev research milestones not found. Skipping portfolio research submissions.");
            return null;
        }

        var wireframe = await EnsureGradedPortfolioSubmissionAsync(
            code: "SUB-PF-WD-WIREFRAME",
            assignmentId: milestoneWireframe.AssignmentId,
            student: student,
            mentor: mentor,
            moduleEnrollment: researchEnrollment,
            milestone: milestoneWireframe,
            contentText: "Responsive wireframe package covering mobile, tablet, and desktop breakpoints for a personal portfolio site.",
            fileUrl: "https://storage.oboxsteam.com/submissions/portfolio/webdev-wireframes-std001.pdf",
            mentorFeedback: "Thoughtful breakpoint strategy and clean grid usage. Approved for build phase.",
            grade: 88m,
            submittedDaysAgo: 30,
            gradedDaysAgo: 28,
            seedTime: seedTime);

        var capstone = await EnsureGradedPortfolioSubmissionAsync(
            code: "SUB-PF-WD-CAPSTONE",
            assignmentId: milestoneCapstone.AssignmentId,
            student: student,
            mentor: mentor,
            moduleEnrollment: researchEnrollment,
            milestone: milestoneCapstone,
            contentText: "Deployed capstone site with source archive: a responsive personal portfolio with dark mode and CMS-driven project pages.",
            fileUrl: "https://storage.oboxsteam.com/submissions/portfolio/webdev-capstone-std001.zip",
            mentorFeedback: "Excellent deployment pipeline and accessibility scores. Capstone passed with distinction.",
            grade: 95m,
            submittedDaysAgo: 10,
            gradedDaysAgo: 7,
            seedTime: seedTime);

        return (wireframe, capstone);
    }

    private async Task<Submission> EnsureGradedPortfolioSubmissionAsync(
        string code,
        Guid assignmentId,
        User student,
        User mentor,
        ModuleEnrollment moduleEnrollment,
        ResearchMilestone milestone,
        string contentText,
        string fileUrl,
        string mentorFeedback,
        decimal grade,
        int submittedDaysAgo,
        int gradedDaysAgo,
        DateTime seedTime)
    {
        var existing = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.Code == code && !s.IsDeleted);
        if (existing != null)
        {
            if (existing.Status != SubmissionStatus.Graded
                || existing.StudentId != student.Id
                || existing.ModuleEnrollmentId != moduleEnrollment.Id)
            {
                existing.StudentId = student.Id;
                existing.ModuleEnrollmentId = moduleEnrollment.Id;
                existing.ResearchMilestoneId = milestone.Id;
                existing.AssignmentId = assignmentId;
                existing.Status = SubmissionStatus.Graded;
                existing.ContentText = contentText;
                existing.FileUrl = fileUrl;
                existing.MentorFeedback = mentorFeedback;
                existing.AssignedGrade = grade;
                existing.SubmittedAt ??= seedTime.AddDays(-submittedDaysAgo);
                existing.GradedAt ??= seedTime.AddDays(-gradedDaysAgo);
                existing.VerifiedBy = mentor.Id;
                await _unitOfWork.Submissions.Update(existing);
                await _unitOfWork.SaveChangesAsync();
            }

            return existing;
        }

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            Code = code,
            AssignmentId = assignmentId,
            StudentId = student.Id,
            ModuleEnrollmentId = moduleEnrollment.Id,
            ResearchMilestoneId = milestone.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            ContentText = contentText,
            FileUrl = fileUrl,
            MentorFeedback = mentorFeedback,
            AssignedGrade = grade,
            SubmittedAt = seedTime.AddDays(-submittedDaysAgo),
            GradedAt = seedTime.AddDays(-gradedDaysAgo),
            VerifiedBy = mentor.Id,
            CreatedAt = seedTime.AddDays(-submittedDaysAgo - 2),
            CreatedBy = student.Id,
            UpdatedAt = seedTime.AddDays(-gradedDaysAgo),
            UpdatedBy = mentor.Id,
            IsDeleted = false,
        };

        await _unitOfWork.Submissions.AddAsync(submission);
        await _unitOfWork.SaveChangesAsync();
        return submission;
    }

    private async Task<Portfolio> SeedStudent1PortfolioAsync(User student, DateTime seedTime)
    {
        var existing = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.StudentId == student.Id && p.ParentPortfolioId == null && !p.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var theme = new
        {
            templateId = "cv-modern",
            primaryColor = "#0F766E",
            secondaryColor = "#134E4A",
            accentColor = "#F59E0B",
            fontFamily = "Inter",
            headingFontFamily = "Space Grotesk",
            fontScale = "base",
            lineHeight = "normal",
            density = "cozy",
            backgroundStyle = "plain",
            cardStyle = "soft",
            layoutStyle = "cv",
            sectionOrder = new[]
            {
                "about",
                "projects",
                "certificates",
                "experience",
                "skills",
            },
        };

        var links = new[]
        {
            new { label = "GitHub", url = "https://github.com/bob-student" },
            new { label = "LinkedIn", url = "https://linkedin.com/in/bob-student" },
        };

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            Code = "OBOX-PF-STD001",
            StudentId = student.Id,
            DisplayName = student.FullName ?? "Bob Student",
            Headline = "STEAM Maker · Web & Creative Tech",
            Tagline = "Building curious projects at the intersection of code and design.",
            Summary =
                "High-school STEAM learner exploring web development, prototyping, and visual storytelling. " +
                "Passionate about turning classroom challenges into portfolio-ready experiments for both tech and design pathways.",
            CoverImageUrl = null,
            ThemeConfig = JsonSerializer.Serialize(theme, PortfolioSeedJsonOptions),
            Links = JsonSerializer.Serialize(links, PortfolioSeedJsonOptions),
            TemplateId = "cv-modern",
            PrimaryColor = "#0F766E",
            PlanType = PlanType.Standard,
            IsPublic = false,
            Subdomain = null,
            HasUnpublishedChanges = false,
            CreatedAt = seedTime,
            CreatedBy = student.Id,
            IsDeleted = false,
        };

        await _unitOfWork.Portfolios.AddAsync(portfolio);
        await _unitOfWork.SaveChangesAsync();
        return portfolio;
    }

    private async Task SeedPortfolioItemsAsync(
        Portfolio portfolio,
        Program programWebDev,
        Module? moduleWebDev1,
        Module moduleWebDev3,
        ProgramEnrollment programEnrollment,
        ModuleEnrollment researchEnrollment,
        List<Certificate> certificates,
        Submission wireframeSubmission,
        Submission capstoneSubmission,
        DateTime seedTime)
    {
        var existingItems = await _unitOfWork.PortfolioCustomItems.GetAllAsync(
            i => i.PortfolioId == portfolio.Id && !i.IsDeleted);
        if (existingItems.Count > 0)
        {
            _loggerService.LogInformation(
                "Portfolio items already exist for {PortfolioCode}, skipping item seeding.",
                portfolio.Code);
            return;
        }

        var displayOrder = 0;
        var items = new List<PortfolioCustomItem>();

        foreach (var certificate in certificates.OrderBy(c => c.ModuleId.HasValue ? 0 : 1))
        {
            var isModuleCert = certificate.ModuleId.HasValue;
            items.Add(new PortfolioCustomItem
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                ItemType = PortfolioItemType.InternalCertificate,
                ReferenceId = certificate.Id,
                ProgramId = certificate.ProgramId,
                ProgramEnrollmentId = programEnrollment.Id,
                ModuleId = certificate.ModuleId,
                Title = isModuleCert
                    ? moduleWebDev1?.Name ?? "Module Certificate"
                    : programWebDev.Name,
                Description = isModuleCert
                    ? "Completed the HTML & CSS foundations module."
                    : "Program completion certificate for the Web Development Bootcamp.",
                MediaUrl = certificate.PdfUrl,
                DisplayOrder = displayOrder++,
                IsVisible = true,
                Source = PortfolioItemSource.AutoImported,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        var capstoneItem = new PortfolioCustomItem
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            ItemType = PortfolioItemType.CapstoneProject,
            SubmissionId = capstoneSubmission.Id,
            ProgramId = programWebDev.Id,
            ProgramEnrollmentId = programEnrollment.Id,
            ModuleId = moduleWebDev3.Id,
            ModuleEnrollmentId = researchEnrollment.Id,
            Title = moduleWebDev3.Name,
            Description = capstoneSubmission.ContentText,
            MentorEndorsement = capstoneSubmission.MentorFeedback,
            MediaUrl = capstoneSubmission.FileUrl,
            DisplayOrder = displayOrder++,
            IsVisible = true,
            Source = PortfolioItemSource.AutoImported,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        items.Add(capstoneItem);

        items.Add(new PortfolioCustomItem
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            ItemType = PortfolioItemType.Project,
            Title = "Personal Portfolio Site v2",
            Subtitle = "Personal project",
            Organization = "Home lab",
            Description =
                "Rebuilt my personal site with a component-driven design system, dark mode, and automated deployments.",
            StudentEditedBody =
                "After the capstone I kept iterating: extracted a small design system, added content collections, and cut load time in half.",
            ExternalUrl = "https://github.com/bob-student/portfolio-v2",
            StartDate = seedTime.AddDays(-50),
            EndDate = seedTime.AddDays(-10),
            DisplayOrder = displayOrder++,
            IsVisible = true,
            Source = PortfolioItemSource.StudentEdited,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });

        items.Add(new PortfolioCustomItem
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            ItemType = PortfolioItemType.Extracurricular,
            Title = "School Maker Club Lead",
            Subtitle = "Club president",
            Organization = "Greenfield High Maker Club",
            Description = "Organize weekly build sessions and mentor younger members on web basics and Arduino.",
            StartDate = seedTime.AddDays(-200),
            EndDate = null,
            DisplayOrder = displayOrder++,
            IsVisible = true,
            Source = PortfolioItemSource.StudentEdited,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });

        items.Add(new PortfolioCustomItem
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            ItemType = PortfolioItemType.Hobby,
            Title = "Digital Illustration Studies",
            Subtitle = "Creative practice",
            Organization = "Self-directed",
            Description =
                "Weekly sketch studies combining technical diagrams with expressive character design — useful for both art and engineering storytelling.",
            MediaUrl = "https://images.unsplash.com/photo-1513364776144-60967b0f800f?q=80&w=800&auto=format&fit=crop",
            StartDate = seedTime.AddDays(-120),
            DisplayOrder = displayOrder++,
            IsVisible = true,
            Source = PortfolioItemSource.StudentEdited,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });

        items.Add(new PortfolioCustomItem
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            ItemType = PortfolioItemType.ExternalCert,
            Title = "Python for Everybody (Coursera)",
            Subtitle = "External certificate",
            Organization = "University of Michigan / Coursera",
            Description = "Completed foundational Python programming specialization.",
            ExternalUrl = "https://www.coursera.org/specializations/python",
            StartDate = seedTime.AddDays(-180),
            EndDate = seedTime.AddDays(-150),
            DisplayOrder = displayOrder,
            IsVisible = true,
            Source = PortfolioItemSource.StudentEdited,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });

        await _unitOfWork.PortfolioCustomItems.AddRangeAsync(items);
        await _unitOfWork.SaveChangesAsync();

        var existingAppendix = await _unitOfWork.PortfolioItemSubmissions.GetAllAsync(
            a => a.PortfolioCustomItemId == capstoneItem.Id && !a.IsDeleted);
        if (existingAppendix.Count == 0)
        {
            await _unitOfWork.PortfolioItemSubmissions.AddAsync(new PortfolioItemSubmission
            {
                Id = Guid.NewGuid(),
                PortfolioCustomItemId = capstoneItem.Id,
                SubmissionId = wireframeSubmission.Id,
                SectionTitle = "Responsive Planning",
                DisplayOrder = 0,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private async Task SeedPortfolioSectionsAsync(Portfolio portfolio, DateTime seedTime)
    {
        var existing = await _unitOfWork.PortfolioSections.GetAllAsync(
            s => s.PortfolioId == portfolio.Id && !s.IsDeleted);
        if (existing.Count > 0)
        {
            _loggerService.LogInformation(
                "Portfolio sections already exist for {PortfolioCode}, skipping section seeding.",
                portfolio.Code);
            return;
        }

        var sections = new List<PortfolioSection>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                Kind = PortfolioSectionKind.ProjectsGroup,
                Title = "Projects",
                DisplayOrder = 0,
                IsVisible = true,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
            new()
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                Kind = PortfolioSectionKind.ActivitiesGroup,
                Title = "Activities",
                DisplayOrder = 1,
                IsVisible = true,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
            new()
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                Kind = PortfolioSectionKind.LinksGroup,
                Title = "Links",
                DisplayOrder = 2,
                IsVisible = true,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
            new()
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolio.Id,
                Kind = PortfolioSectionKind.RichText,
                Title = "About my journey",
                DisplayOrder = 3,
                IsVisible = true,
                ContentHtml =
                    "<p>I build STEAM projects that mix <strong>code</strong> and <em>design</em>.</p>",
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
        };

        await _unitOfWork.PortfolioSections.AddRangeAsync(sections);
        await _unitOfWork.SaveChangesAsync();
    }
}
