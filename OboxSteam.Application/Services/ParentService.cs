using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.AuthDTO;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.DTOs.ParentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class ParentService : IParentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ParentService> _logger;
    private readonly INotificationPublisher _notificationPublisher;

    public ParentService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IClaimsService claimsService,
        ILogger<ParentService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<bool> RequestParentLinkAsync(RequestLinkDto dto, IConfiguration configuration)
    {
        var studentId = _claimsService.GetCurrentUserId;
        if (studentId == Guid.Empty)
        {
            _logger.LogWarning("Unauthorized attempt to request parent link.");
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var parentEmail = dto.ParentEmail.ToLower();
        _logger.LogInformation("Student {StudentId} is requesting a link with parent {ParentEmail}.", studentId, parentEmail);

        // Check if student already has 2 parent links
        var activeLinks = await _unitOfWork.ParentStudents.GetAllAsync(ps => ps.StudentId == studentId && !ps.IsDeleted);

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == parentEmail && u.Role == RoleType.Parent);

        if (activeLinks.Count >= 2)
        {
            var isAlreadyLinked = parent != null && activeLinks.Any(ps => ps.ParentId == parent.Id);
            if (!isAlreadyLinked)
            {
                _logger.LogWarning("Student {StudentId} attempted to link with a 3rd parent {ParentEmail}.", studentId, parentEmail);
                throw ErrorHelper.BadRequest("Each student is only allowed to link with a maximum of 2 parent accounts.");
            }
        }

        if (parent == null)
        {
            _logger.LogInformation("Parent account with email {ParentEmail} does not exist. Creating shadow account.", parentEmail);
            
            // TẠO SHADOW ACCOUNT
            parent = new User
            {
                Code = $"PRT-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                Email = parentEmail,
                Role = RoleType.Parent,
                PasswordHash = null,
                IsEmailVerified = false,
                Status = AccountStatus.Active
            };
            await _unitOfWork.Users.AddAsync(parent);
            
            // Tạo liên kết chờ (Pending)
            var parentStudentDto = new ParentStudentCreateDto { ParentId = parent.Id, StudentId = studentId, IsVerified = false };
            var parentStudentEntity = new ParentStudent { ParentId = parentStudentDto.ParentId, StudentId = parentStudentDto.StudentId, IsVerified = parentStudentDto.IsVerified };
            
            await _unitOfWork.ParentStudents.AddAsync(parentStudentEntity);
            await _unitOfWork.SaveChangesAsync();

            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ParentLinkRequested(parent.Id, studentId, actorUserId: studentId));

            _logger.LogInformation("Created shadow account and pending link for parent {ParentEmail} (ParentId: {ParentId}).", parentEmail, parent.Id);

            // Vô hiệu hóa các OTP MagicLink/ApproveLink cũ của email này tạo bởi học sinh này nếu có
            var previousOtps = await _unitOfWork.OtpStorages.GetAllAsync(o =>
                o.Target == parentEmail &&
                o.CreatedBy == studentId &&
                (o.Purpose == OtpPurpose.MagicLink || o.Purpose == OtpPurpose.ApproveLink) &&
                !o.IsUsed);

            foreach (var previousOtp in previousOtps)
            {
                previousOtp.IsUsed = true;
                await _unitOfWork.OtpStorages.Update(previousOtp);
            }

            // Sinh Token ngẫu nhiên (10 ký tự)
            var token = OtpGenerator.GenerateAlphanumeric(10);
            var otp = new OtpStorage
            {
                Target = parentEmail,
                OtpCode = token,
                ExpiredAt = DateTime.UtcNow.AddHours(24),
                IsUsed = false,
                Purpose = OtpPurpose.MagicLink,
                CreatedBy = studentId
            };

            await _unitOfWork.OtpStorages.AddAsync(otp);
            await _unitOfWork.SaveChangesAsync();

            var appBaseUrl = (configuration["APP_BASE_URL"] ?? "https://oboxsteam.website").TrimEnd('/');
            var loginUrl = $"{appBaseUrl}/magic-login?email={Uri.EscapeDataString(parentEmail)}&token={token}";

            await _emailService.SendMagicLinkEmailAsync(new ActionEmailRequestDto { To = parentEmail, Link = loginUrl });
            _logger.LogInformation("Sent magic login link email to new parent {ParentEmail}.", parentEmail);
            return true;
        }

        // ĐÃ TỒN TẠI PARENT -> Kiểm tra liên kết
        var parentStudent = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(ps => ps.ParentId == parent.Id && ps.StudentId == studentId);
        if (parentStudent != null && parentStudent.IsVerified)
        {
            _logger.LogWarning("Conflict: Link between student {StudentId} and parent {ParentEmail} already exists and is verified.", studentId, parentEmail);
            throw ErrorHelper.Conflict("This parent is already associated with you.");
        }
        else if (parentStudent == null)
        {
            _logger.LogInformation("No relation exists. Creating pending link between student {StudentId} and parent {ParentId}.", studentId, parent.Id);
            
            // Chưa có liên kết -> Insert pending link
            var parentStudentDto = new ParentStudentCreateDto { ParentId = parent.Id, StudentId = studentId, IsVerified = false };
            var parentStudentEntity = new ParentStudent { ParentId = parentStudentDto.ParentId, StudentId = parentStudentDto.StudentId, IsVerified = parentStudentDto.IsVerified };
            await _unitOfWork.ParentStudents.AddAsync(parentStudentEntity);
            await _unitOfWork.SaveChangesAsync();

            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ParentLinkRequested(parent.Id, studentId, actorUserId: studentId));
        }

        // Vô hiệu hóa các OTP MagicLink/ApproveLink cũ của email này tạo bởi học sinh này nếu có
        var existingOtps = await _unitOfWork.OtpStorages.GetAllAsync(o =>
            o.Target == parentEmail &&
            o.CreatedBy == studentId &&
            (o.Purpose == OtpPurpose.MagicLink || o.Purpose == OtpPurpose.ApproveLink) &&
            !o.IsUsed);

        foreach (var previousOtp in existingOtps)
        {
            previousOtp.IsUsed = true;
            await _unitOfWork.OtpStorages.Update(previousOtp);
        }

        // Sinh Token ngẫu nhiên (10 ký tự)
        var parentToken = OtpGenerator.GenerateAlphanumeric(10);
        var purpose = string.IsNullOrEmpty(parent.PasswordHash) ? OtpPurpose.MagicLink : OtpPurpose.ApproveLink;
        var parentOtp = new OtpStorage
        {
            Target = parentEmail,
            OtpCode = parentToken,
            ExpiredAt = DateTime.UtcNow.AddHours(24),
            IsUsed = false,
            Purpose = purpose,
            CreatedBy = studentId
        };

        await _unitOfWork.OtpStorages.AddAsync(parentOtp);
        await _unitOfWork.SaveChangesAsync();

        var baseAppUrl = (configuration["APP_BASE_URL"] ?? "https://oboxsteam.website").TrimEnd('/');

        if (purpose == OtpPurpose.MagicLink)
        {
            _logger.LogInformation("Parent {ParentEmail} has a shadow account (no password). Sending magic link.", parentEmail);
            var loginUrl = $"{baseAppUrl}/magic-login?email={Uri.EscapeDataString(parentEmail)}&token={parentToken}";
            await _emailService.SendMagicLinkEmailAsync(new ActionEmailRequestDto { To = parentEmail, Link = loginUrl });
            _logger.LogInformation("Sent magic login link email to shadow parent {ParentEmail}.", parentEmail);
        }
        else
        {
            _logger.LogInformation("Parent {ParentEmail} has a completed account. Sending approval link.", parentEmail);
            var approveUrl = $"{baseAppUrl}/approve-link?email={Uri.EscapeDataString(parentEmail)}&token={parentToken}";
            await _emailService.SendApproveLinkEmailAsync(new ActionEmailRequestDto { To = parentEmail, Link = approveUrl });
            _logger.LogInformation("Sent approval link email to registered parent {ParentEmail}.", parentEmail);
        }

        return true;
    }

    public async Task<LoginResponseDto> MagicLoginAsync(MagicLoginDto dto, IConfiguration configuration)
    {
        _logger.LogInformation("Magic login attempt initiated for email {Email}.", dto.Email);
        
        // Find and validate the OTP in the database (do NOT mark as used here)
        var otp = await _unitOfWork.OtpStorages.FirstOrDefaultAsync(o => 
            o.Target == dto.Email && 
            o.OtpCode == dto.Token && 
            o.Purpose == OtpPurpose.MagicLink && 
            !o.IsUsed);

        if (otp == null || otp.ExpiredAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Magic login failed: Invalid or expired token.");
            throw ErrorHelper.BadRequest("Invalid or expired magic link.");
        }

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && !u.IsDeleted);
        if (parent == null)
        {
            _logger.LogWarning("Magic login failed: Account for Parent Email {Email} not found.", dto.Email);
            throw ErrorHelper.NotFound("Account does not exist.");
        }
        if (parent.Status == AccountStatus.Locked)
        {
            _logger.LogWarning("Magic login failed: Parent ID {ParentId} is locked.", parent.Id);
            throw ErrorHelper.Forbidden("Account has been locked.");
        }

        // OTP is intentionally NOT marked as used here.
        // The token remains valid for 24 hours and will only be consumed
        // once the parent completes their profile via CompleteProfileAsync.
        _logger.LogInformation("Magic login validated for Parent {ParentEmail}. Issuing tokens (OTP kept active until profile completion).", parent.Email);
        return await IssueTokensAsync(parent, configuration);
    }

    public async Task<bool> CompleteProfileAsync(CompleteProfileDto dto)
    {
        var parentId = _claimsService.GetCurrentUserId;
        if (parentId == Guid.Empty)
        {
            _logger.LogWarning("Unauthorized attempt to complete parent profile.");
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        _logger.LogInformation("Completing profile for Parent ID {ParentId}.", parentId);

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == parentId && !u.IsDeleted);
        if (parent == null)
        {
            _logger.LogWarning("Complete profile failed: Parent ID {ParentId} not found or deleted.", parentId);
            throw ErrorHelper.NotFound("Account does not exist.");
        }

        parent.FullName = dto.FullName;
        parent.Phone = dto.Phone;
        parent.PasswordHash = new PasswordHasher().HashPassword(dto.Password);
        parent.IsEmailVerified = true;
        
        await _unitOfWork.Users.Update(parent);

        // Consume the active MagicLink OTP and confirm the parent-student association.
        // This is the real business event: the parent has completed their profile,
        // meaning they genuinely used the magic link.
        var activeOtp = await _unitOfWork.OtpStorages.FirstOrDefaultAsync(o =>
            o.Target == parent.Email &&
            o.Purpose == OtpPurpose.MagicLink &&
            !o.IsUsed);

        if (activeOtp != null)
        {
            activeOtp.IsUsed = true;
            await _unitOfWork.OtpStorages.Update(activeOtp);
            _logger.LogInformation("MagicLink OTP consumed for Parent ID {ParentId} upon profile completion.", parentId);

            // Confirm the parent-student link that was pending this magic login
            var targetStudentId = activeOtp.CreatedBy;
            if (targetStudentId != Guid.Empty)
            {
                var ps = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(x =>
                    x.ParentId == parent.Id && x.StudentId == targetStudentId);
                if (ps != null && !ps.IsVerified)
                {
                    ps.IsVerified = true;
                    await _unitOfWork.ParentStudents.Update(ps);
                    _logger.LogInformation("Link verified between Parent {ParentId} and Student {StudentId} upon profile completion.", parent.Id, targetStudentId);
                }
            }
        }
        else
        {
            _logger.LogWarning("No active MagicLink OTP found for Parent ID {ParentId} during profile completion.", parentId);
        }

        await _unitOfWork.SaveChangesAsync();

        if (activeOtp?.CreatedBy is Guid verifiedStudentId && verifiedStudentId != Guid.Empty)
        {
            await _notificationPublisher.PublishManyAsync(new[]
            {
                NotificationCatalog.ParentLinkVerified(parent.Id, verifiedStudentId),
                NotificationCatalog.ParentLinkApproved(verifiedStudentId, parent.Id, actorUserId: parent.Id)
            });
        }
        
        _logger.LogInformation("Profile successfully completed and activated for Parent ID {ParentId}.", parentId);
        return true;
    }

    public async Task<bool> ApproveLinkAsync(ApproveLinkDto dto, IConfiguration configuration)
    {
        var currentUserId = _claimsService.GetCurrentUserId;
        if (currentUserId == Guid.Empty)
        {
            _logger.LogWarning("Unauthorized attempt to approve link.");
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        _logger.LogInformation("Approve link requested by User ID {CurrentUserId}.", currentUserId);

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == currentUserId && !u.IsDeleted);
        if (parent == null || parent.Role != RoleType.Parent)
        {
            _logger.LogWarning("Approve link failed: User {UserId} is not a valid active Parent.", currentUserId);
            throw ErrorHelper.Forbidden("Only registered parents can approve associations.");
        }

        // Find and validate the OTP in the database
        var otp = await _unitOfWork.OtpStorages.FirstOrDefaultAsync(o => 
            o.Target == parent.Email && 
            o.OtpCode == dto.Token && 
            o.Purpose == OtpPurpose.ApproveLink && 
            !o.IsUsed);

        if (otp == null || otp.ExpiredAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Approve link failed: Invalid or expired token.");
            throw ErrorHelper.BadRequest("Invalid or expired link.");
        }

        // Mark OTP as used
        otp.IsUsed = true;
        await _unitOfWork.OtpStorages.Update(otp);

        // Bật cờ xác nhận cho link
        var targetStudentId = otp.CreatedBy;
        if (targetStudentId != Guid.Empty)
        {
            _logger.LogInformation("Verifying link between Parent ID {ParentId} and Student ID {StudentId} via manual approval.", parent.Id, targetStudentId);
            var ps = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(x => x.ParentId == parent.Id && x.StudentId == targetStudentId);
            if (ps != null && !ps.IsVerified)
            {
                ps.IsVerified = true;
                await _unitOfWork.ParentStudents.Update(ps);
                _logger.LogInformation("Link verified successfully between Parent {ParentId} and Student {StudentId}.", parent.Id, targetStudentId);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        if (targetStudentId != Guid.Empty)
        {
            await _notificationPublisher.PublishManyAsync(new[]
            {
                NotificationCatalog.ParentLinkVerified(parent.Id, targetStudentId),
                NotificationCatalog.ParentLinkApproved(targetStudentId, parent.Id, actorUserId: parent.Id)
            });
        }

        return true;
    }

    public async Task<List<ParentStudentRelationDto>> GetParentStudentRelationsAsync()
    {
        var currentUserId = _claimsService.GetCurrentUserId;
        if (currentUserId == Guid.Empty)
        {
            _logger.LogWarning("Unauthorized attempt to retrieve parent-student relations.");
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        _logger.LogInformation("Retrieving parent-student relations for User ID {CurrentUserId}.", currentUserId);

        var user = await _unitOfWork.Users.GetByIdAsync(currentUserId);
        if (user == null || user.IsDeleted)
        {
            _logger.LogWarning("Fetch relations failed: User ID {CurrentUserId} not found or deleted.", currentUserId);
            throw ErrorHelper.NotFound("User account does not exist.");
        }

        if (user.Role == RoleType.Student)
        {
            _logger.LogInformation("Retrieving parent links for Student ID {CurrentUserId}.", currentUserId);
            var links = await _unitOfWork.ParentStudents.GetAllAsync(
                ps => ps.StudentId == currentUserId && !ps.IsDeleted,
                ps => ps.Parent
            );

            _logger.LogInformation("Retrieved {Count} parent links for Student ID {CurrentUserId}.", links.Count, currentUserId);
            return links.Select(ps => new ParentStudentRelationDto
            {
                LinkedUserId = ps.ParentId,
                Code = ps.Parent.Code,
                Email = ps.Parent.Email,
                FullName = ps.Parent.FullName,
                Phone = ps.Parent.Phone,
                AvatarUrl = ps.Parent.AvatarUrl,
                IsVerified = ps.IsVerified,
                CreatedAt = ps.CreatedAt
            }).ToList();
        }
        else if (user.Role == RoleType.Parent)
        {
            _logger.LogInformation("Retrieving student links for Parent ID {CurrentUserId}.", currentUserId);
            var links = await _unitOfWork.ParentStudents.GetAllAsync(
                ps => ps.ParentId == currentUserId && !ps.IsDeleted,
                ps => ps.Student
            );

            _logger.LogInformation("Retrieved {Count} student links for Parent ID {CurrentUserId}.", links.Count, currentUserId);
            return links.Select(ps => new ParentStudentRelationDto
            {
                LinkedUserId = ps.StudentId,
                Code = ps.Student.Code,
                Email = ps.Student.Email,
                FullName = ps.Student.FullName,
                Phone = ps.Student.Phone,
                AvatarUrl = ps.Student.AvatarUrl,
                IsVerified = ps.IsVerified,
                CreatedAt = ps.CreatedAt
            }).ToList();
        }

        _logger.LogWarning("Fetch relations forbidden: User ID {CurrentUserId} has unsupported role {RoleType}.", currentUserId, user.Role);
        throw ErrorHelper.Forbidden("Only students and parents can view relationship connections.");
    }

    private async Task<LoginResponseDto> IssueTokensAsync(User user, IConfiguration configuration)
    {
        _logger.LogInformation("Issuing access and refresh tokens for User ID {UserId} ({Email}).", user.Id, user.Email);
        
        var accessToken = JwtUtils.GenerateJwtToken(user.Id, user.Email, user.Role.ToString(), configuration, TimeSpan.FromMinutes(30));
        var refreshToken = TokenTools.GenerateRefreshToken();
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Tokens successfully issued and database updated for User ID {UserId}.", user.Id);
        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }
}
