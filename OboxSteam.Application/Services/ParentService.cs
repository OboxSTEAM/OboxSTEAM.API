using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.AuthDTO;
using OboxSteam.Application.DTOs.EmailDTO;
using OboxSteam.Application.DTOs.ParentDTO;
using OboxSteam.Application.Interfaces;
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

    public ParentService(IUnitOfWork unitOfWork, IEmailService emailService, IClaimsService claimsService, ILogger<ParentService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _claimsService = claimsService;
        _logger = logger;
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

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == parentEmail && u.Role == RoleType.Parent);

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

            _logger.LogInformation("Created shadow account and pending link for parent {ParentEmail} (ParentId: {ParentId}).", parentEmail, parent.Id);

            // Sinh Magic Token vì đây là lần đầu (nhúng kèm StudentId để duyệt)
            var magicToken = JwtUtils.GenerateActionToken(parent.Id, "MagicLink", studentId.ToString(), configuration, TimeSpan.FromHours(24));
            var loginUrl = $"{configuration["APP_BASE_URL"]}/magic-login?token={magicToken}";

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
        }

        if (string.IsNullOrEmpty(parent.PasswordHash))
        {
            _logger.LogInformation("Parent {ParentEmail} has a shadow account (no password). Sending magic link.", parentEmail);
            
            // Tài khoản ngầm (Shadow) -> Yêu cầu đăng nhập Magic Link
            var magicToken = JwtUtils.GenerateActionToken(parent.Id, "MagicLink", studentId.ToString(), configuration, TimeSpan.FromHours(24));
            var loginUrl = $"{configuration["APP_BASE_URL"]}/magic-login?token={magicToken}";

            await _emailService.SendMagicLinkEmailAsync(new ActionEmailRequestDto { To = parentEmail, Link = loginUrl });
            _logger.LogInformation("Sent magic login link email to shadow parent {ParentEmail}.", parentEmail);
        }
        else
        {
            _logger.LogInformation("Parent {ParentEmail} has a completed account. Sending approval link.", parentEmail);
            
            // Phụ huynh đã có tài khoản chuẩn => Gửi ApproveLink
            var approveToken = JwtUtils.GenerateActionToken(parent.Id, "ApproveLink", studentId.ToString(), configuration, TimeSpan.FromHours(24));
            var approveUrl = $"{configuration["APP_BASE_URL"]}/approve-link?token={approveToken}";

            await _emailService.SendApproveLinkEmailAsync(new ActionEmailRequestDto { To = parentEmail, Link = approveUrl });
            _logger.LogInformation("Sent approval link email to registered parent {ParentEmail}.", parentEmail);
        }

        return true;
    }

    public async Task<LoginResponseDto> MagicLoginAsync(MagicLoginDto dto, IConfiguration configuration)
    {
        _logger.LogInformation("Magic login attempt initiated.");
        
        var principal = JwtUtils.ValidateActionToken(dto.Token, configuration);
        if (principal.FindFirst("Purpose")?.Value != "MagicLink")
        {
            _logger.LogWarning("Magic login failed: Invalid token purpose.");
            throw ErrorHelper.BadRequest("Invalid token for login.");
        }

        var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("nameid")?.Value ?? principal.FindFirst("sub")?.Value;
        var studentIdStr = principal.FindFirst("ExtraData")?.Value;

        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parentId))
        {
            _logger.LogWarning("Magic login failed: Token does not contain valid User ID.");
            throw ErrorHelper.BadRequest("Token does not contain User information.");
        }

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == parentId && !u.IsDeleted);
        if (parent == null)
        {
            _logger.LogWarning("Magic login failed: Account for Parent ID {ParentId} not found or is deleted.", parentId);
            throw ErrorHelper.NotFound("Account does not exist.");
        }
        if (parent.Status == AccountStatus.Locked)
        {
            _logger.LogWarning("Magic login failed: Parent ID {ParentId} is locked.", parentId);
            throw ErrorHelper.Forbidden("Account has been locked.");
        }

        // Bật cờ xác nhận liên kết cho bé thứ 1
        if (!string.IsNullOrEmpty(studentIdStr) && Guid.TryParse(studentIdStr, out var studentId))
        {
            _logger.LogInformation("Confirming link association between Parent ID {ParentId} and Student ID {StudentId} via magic login.", parent.Id, studentId);
            var ps = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(x => x.ParentId == parent.Id && x.StudentId == studentId);
            if (ps != null && !ps.IsVerified)
            {
                ps.IsVerified = true;
                await _unitOfWork.ParentStudents.Update(ps);
                _logger.LogInformation("Link verified successfully between Parent {ParentId} and Student {StudentId}.", parent.Id, studentId);
            }
        }
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Magic login successful for Parent {ParentEmail}. Issuing tokens.", parent.Email);
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
        await _unitOfWork.SaveChangesAsync();
        
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

        var principal = JwtUtils.ValidateActionToken(dto.Token, configuration);
        if (principal.FindFirst("Purpose")?.Value != "ApproveLink")
        {
            _logger.LogWarning("Approve link failed: Invalid token purpose.");
            throw ErrorHelper.BadRequest("Invalid token.");
        }

        var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("nameid")?.Value ?? principal.FindFirst("sub")?.Value;
        var studentIdStr = principal.FindFirst("ExtraData")?.Value;
        
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parentId))
        {
            _logger.LogWarning("Approve link failed: Token does not contain valid User ID.");
            throw ErrorHelper.BadRequest("Invalid token format.");
        }

        if (currentUserId != parentId)
        {
            _logger.LogWarning("Approve link forbidden: Current User ID {CurrentUserId} does not match token Parent ID {ParentId}.", currentUserId, parentId);
            throw ErrorHelper.Forbidden("You do not have permission to approve this association.");
        }

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == parentId && !u.IsDeleted);
        if (parent == null)
        {
            _logger.LogWarning("Approve link failed: Parent ID {ParentId} not found or deleted.", parentId);
            throw ErrorHelper.NotFound("Account does not exist.");
        }

        // Bật cờ xác nhận cho link
        if (!string.IsNullOrEmpty(studentIdStr) && Guid.TryParse(studentIdStr, out var studentId))
        {
            _logger.LogInformation("Verifying link between Parent ID {ParentId} and Student ID {StudentId} via manual approval.", parent.Id, studentId);
            var ps = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(x => x.ParentId == parent.Id && x.StudentId == studentId);
            if (ps != null && !ps.IsVerified)
            {
                ps.IsVerified = true;
                await _unitOfWork.ParentStudents.Update(ps);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Link verified successfully between Parent {ParentId} and Student {StudentId}.", parent.Id, studentId);
            }
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
