using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CertificateDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class CertificateService : ICertificateService
{
    private const string CertificatesRootFolder = "certificates";
    private const string ViewForbiddenMessage = "You do not have permission to view this certificate.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IBlobService _blobService;
    private readonly ICertificatePdfGenerator _pdfGenerator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CertificateService> _logger;

    public CertificateService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IBlobService blobService,
        ICertificatePdfGenerator pdfGenerator,
        IConfiguration configuration,
        ILogger<CertificateService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _blobService = blobService;
        _pdfGenerator = pdfGenerator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CertificateDetailDto?> EnsureProgramCertificateAsync(Guid programEnrollmentId)
    {
        if (programEnrollmentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Program enrollment id is required.");
        }

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program enrollment with id '{programEnrollmentId}' not found.");
        }

        await EnsureCallerCanIssueAsync(enrollment.StudentId);

        if (!await AreAllProgramActivitiesDoneAsync(enrollment))
        {
            _logger.LogInformation(
                "[EnsureProgramCertificateAsync] Enrollment {EnrollmentId} not eligible — activities incomplete.",
                programEnrollmentId);
            return null;
        }

        var existing = await _unitOfWork.Certificates.FirstOrDefaultAsync(
            c => c.StudentId == enrollment.StudentId
                 && c.ProgramId == enrollment.ProgramId
                 && c.ModuleId == null
                 && !c.IsDeleted);

        var program = await _unitOfWork.Programs.GetByIdAsync(enrollment.ProgramId);
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{enrollment.ProgramId}' not found.");
        }

        var student = await _unitOfWork.Users.GetByIdAsync(enrollment.StudentId);
        if (student == null || student.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Student with id '{enrollment.StudentId}' not found.");
        }

        var modules = (await _unitOfWork.Modules.GetAllAsync(
                m => m.ProgramId == program.Id && !m.IsDeleted))
            .OrderBy(m => m.ModuleOrder)
            .ToList();

        var certificate = existing;
        if (certificate == null)
        {
            certificate = new Certificate
            {
                Code = await GenerateUniqueCodeAsync(),
                StudentId = enrollment.StudentId,
                ProgramId = enrollment.ProgramId,
                ModuleId = null,
                IssueDate = DateTime.UtcNow,
                SkillsAcquired = program.SkillsGained,
            };
            await _unitOfWork.Certificates.AddAsync(certificate);
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            certificate.IssueDate ??= DateTime.UtcNow;
            certificate.SkillsAcquired ??= program.SkillsGained;
            await _unitOfWork.Certificates.Update(certificate);
            await _unitOfWork.SaveChangesAsync();
        }

        var verificationUrl = BuildVerificationUrl(certificate.Code);
        certificate.VerificationUrl = verificationUrl;

        try
        {
            var pdfBytes = _pdfGenerator.Generate(new CertificatePdfModel
            {
                Code = certificate.Code,
                StudentFullName = string.IsNullOrWhiteSpace(student.FullName)
                    ? student.Email
                    : student.FullName!,
                StudentAvatarUrl = student.AvatarUrl,
                IssuerLogoUrl = CertificateBranding.IssuerLogoUrl,
                ProgramName = program.Name,
                ProgramDescription = program.Description,
                ProgramThumbnailUrl = program.ThumbnailUrl,
                IssueDate = certificate.IssueDate ?? DateTime.UtcNow,
                VerificationUrl = verificationUrl,
                ModuleNames = modules.Select(m => m.Name).ToList(),
            });

            var folder = $"{CertificatesRootFolder}/{program.Id}/{student.Id}";
            var fileName = $"{certificate.Code}.pdf";
            await using var stream = new MemoryStream(pdfBytes);
            await _blobService.UploadFileAsync(fileName, stream, folder);

            var s3Key = $"{folder}/{fileName}";
            certificate.PdfUrl = await _blobService.GetPreviewUrlAsync(s3Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[EnsureProgramCertificateAsync] PDF/S3 failed for enrollment {EnrollmentId}, certificate {Code}.",
                programEnrollmentId,
                certificate.Code);
        }

        await _unitOfWork.Certificates.Update(certificate);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[EnsureProgramCertificateAsync] Certificate {Code} ensured for enrollment {EnrollmentId}.",
            certificate.Code,
            programEnrollmentId);

        return await MapDetailAsync(certificate);
    }

    public async Task<List<CertificateListItemDto>> GetMyCertificatesAsync()
    {
        var currentUser = await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ViewForbiddenMessage);

        IQueryable<Certificate> query = _unitOfWork.Certificates
            .GetQueryable()
            .Where(c => !c.IsDeleted && c.ModuleId == null && c.ProgramId != null);

        if (currentUser.Role == RoleType.Student)
        {
            query = query.Where(c => c.StudentId == currentUser.Id);
        }
        else if (currentUser.Role == RoleType.Parent)
        {
            var linkedIds = await GetLinkedStudentIdsAsync(currentUser.Id);
            query = query.Where(c => linkedIds.Contains(c.StudentId));
        }
        else if (currentUser.Role is not (RoleType.Admin or RoleType.Manager))
        {
            throw ErrorHelper.Forbidden(ViewForbiddenMessage);
        }

        var items = query
            .OrderByDescending(c => c.IssueDate)
            .ThenByDescending(c => c.CreatedAt)
            .ToList();

        var programIds = items
            .Where(c => c.ProgramId.HasValue)
            .Select(c => c.ProgramId!.Value)
            .Distinct()
            .ToList();

        var programs = await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id) && !p.IsDeleted);
        var programsById = programs.ToDictionary(p => p.Id);

        return items.Select(c =>
        {
            programsById.TryGetValue(c.ProgramId!.Value, out var program);
            return new CertificateListItemDto
            {
                Id = c.Id,
                Code = c.Code,
                ProgramId = c.ProgramId.Value,
                ProgramName = program?.Name ?? string.Empty,
                IssueDate = c.IssueDate,
                PdfUrl = c.PdfUrl,
                VerificationUrl = c.VerificationUrl,
            };
        }).ToList();
    }

    public async Task<CertificateDetailDto> GetCertificateByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Certificate id is required.");
        }

        var certificate = await _unitOfWork.Certificates.GetByIdAsync(id);
        if (certificate == null || certificate.IsDeleted || certificate.ModuleId != null)
        {
            throw ErrorHelper.NotFound($"Certificate with id '{id}' not found.");
        }

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            certificate.StudentId,
            ViewForbiddenMessage);

        return await MapDetailAsync(certificate);
    }

    public async Task<CertificateDetailDto?> GetCertificateByEnrollmentAsync(Guid programEnrollmentId)
    {
        if (programEnrollmentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Program enrollment id is required.");
        }

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program enrollment with id '{programEnrollmentId}' not found.");
        }

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            enrollment.StudentId,
            ViewForbiddenMessage);

        var certificate = await _unitOfWork.Certificates.FirstOrDefaultAsync(
            c => c.StudentId == enrollment.StudentId
                 && c.ProgramId == enrollment.ProgramId
                 && c.ModuleId == null
                 && !c.IsDeleted);

        if (certificate == null)
        {
            return null;
        }

        return await MapDetailAsync(certificate);
    }

    public async Task<CertificateDetailDto> GetCertificateByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw ErrorHelper.BadRequest("Certificate code is required.");
        }

        var normalized = code.Trim();
        var certificate = await _unitOfWork.Certificates.FirstOrDefaultAsync(
            c => c.Code == normalized && !c.IsDeleted && c.ModuleId == null);

        if (certificate == null)
        {
            throw ErrorHelper.NotFound($"Certificate with code '{normalized}' not found.");
        }

        return await MapDetailAsync(certificate);
    }

    private async Task EnsureCallerCanIssueAsync(Guid studentId)
    {
        var currentUserId = _claimsService.GetCurrentUserId;
        if (currentUserId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        if (currentUserId == studentId)
        {
            return;
        }

        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId);
        if (currentUser == null || currentUser.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        if (currentUser.Role is RoleType.Admin or RoleType.Manager)
        {
            return;
        }

        throw ErrorHelper.Forbidden("You can only issue certificates for your own enrollments.");
    }

    private async Task<bool> AreAllProgramActivitiesDoneAsync(ProgramEnrollment enrollment)
    {
        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == enrollment.ProgramId && !m.IsDeleted);

        if (modules.Count == 0)
        {
            return false;
        }

        var allActivityIds = new HashSet<Guid>();
        foreach (var module in modules)
        {
            var activityIds = await ActivityProgressCalculationHelper.GetModuleActivityIdsAsync(
                _unitOfWork,
                module.Id);
            foreach (var activityId in activityIds)
            {
                allActivityIds.Add(activityId);
            }
        }

        if (allActivityIds.Count == 0)
        {
            return false;
        }

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == enrollment.Id
                  && me.StudentId == enrollment.StudentId
                  && !me.IsDeleted);

        var latestByModule = moduleEnrollments
            .GroupBy(me => me.ModuleId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(me => me.AttemptNumber).First());

        if (latestByModule.Count == 0)
        {
            return false;
        }

        var moduleEnrollmentIds = latestByModule.Values.Select(me => me.Id).ToList();
        var doneProgresses = await _unitOfWork.ActivityProgresses.GetAllAsync(
            ap => moduleEnrollmentIds.Contains(ap.ModuleEnrollmentId)
                  && allActivityIds.Contains(ap.ActivityId)
                  && ap.ActivityStatus == ActivityStatus.Done
                  && !ap.IsDeleted);

        var doneActivityIds = doneProgresses.Select(ap => ap.ActivityId).Distinct().ToHashSet();
        return doneActivityIds.Count == allActivityIds.Count;
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
            var code = $"OBOX-CERT-{suffix}";
            var collision = await _unitOfWork.Certificates.FirstOrDefaultAsync(c => c.Code == code);
            if (collision == null)
            {
                return code;
            }
        }

        throw ErrorHelper.Internal("Failed to generate a unique certificate code.");
    }

    private string BuildVerificationUrl(string code)
    {
        var frontendBaseUrl = (_configuration["APP_FRONTEND_URL"]
                               ?? _configuration["APP_BASE_URL"]
                               ?? "https://oboxsteam.website").TrimEnd('/');
        return $"{frontendBaseUrl}/certificates/verify/{code}";
    }

    private async Task<List<Guid>> GetLinkedStudentIdsAsync(Guid parentId)
    {
        var links = await _unitOfWork.ParentStudents.GetAllAsync(
            ps => ps.ParentId == parentId && !ps.IsDeleted);
        return links.Select(ps => ps.StudentId).Distinct().ToList();
    }

    private async Task<CertificateDetailDto> MapDetailAsync(Certificate certificate)
    {
        if (!certificate.ProgramId.HasValue)
        {
            throw ErrorHelper.BadRequest("Certificate is not linked to a program.");
        }

        var program = await _unitOfWork.Programs.GetByIdAsync(certificate.ProgramId.Value);
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{certificate.ProgramId}' not found.");
        }

        var student = await _unitOfWork.Users.GetByIdAsync(certificate.StudentId);
        if (student == null || student.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Student with id '{certificate.StudentId}' not found.");
        }

        var modules = (await _unitOfWork.Modules.GetAllAsync(
                m => m.ProgramId == program.Id && !m.IsDeleted))
            .OrderBy(m => m.ModuleOrder)
            .ToList();

        var learningOutcomes = new List<string>();
        var seenOutcomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules)
        {
            foreach (var outcome in module.LearningOutcomes)
            {
                if (string.IsNullOrWhiteSpace(outcome))
                {
                    continue;
                }

                var trimmed = outcome.Trim();
                if (seenOutcomes.Add(trimmed))
                {
                    learningOutcomes.Add(trimmed);
                }
            }
        }

        var skillsSource = certificate.SkillsAcquired ?? program.SkillsGained;
        var skillsGained = ParseSkillsGained(skillsSource);

        return new CertificateDetailDto
        {
            Id = certificate.Id,
            Code = certificate.Code,
            IssueDate = certificate.IssueDate,
            PdfUrl = certificate.PdfUrl,
            VerificationUrl = certificate.VerificationUrl,
            SkillsAcquired = certificate.SkillsAcquired,
            IssuerName = CertificateBranding.IssuerName,
            IssuerLogoUrl = CertificateBranding.IssuerLogoUrl,
            Student = new CertificateStudentDto
            {
                Id = student.Id,
                FullName = student.FullName,
                AvatarUrl = student.AvatarUrl,
            },
            Program = new CertificateProgramDto
            {
                Id = program.Id,
                Name = program.Name,
                Description = program.Description,
                EstimatedDuration = program.EstimatedDuration,
                ThumbnailUrl = program.ThumbnailUrl,
            },
            Modules = modules.Select(m => new CertificateModuleDto
            {
                ModuleId = m.Id,
                Name = m.Name,
                ModuleOrder = m.ModuleOrder,
            }).ToList(),
            LearningOutcomes = learningOutcomes,
            SkillsGained = skillsGained,
        };
    }

    internal static List<string> ParseSkillsGained(string? skillsGained)
    {
        if (string.IsNullOrWhiteSpace(skillsGained))
        {
            return [];
        }

        var trimmed = skillsGained.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(trimmed);
                if (parsed != null)
                {
                    return parsed
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .ToList();
                }
            }
            catch (JsonException)
            {
                // Fall through to comma-separated parsing.
            }
        }

        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToList();
    }
}
