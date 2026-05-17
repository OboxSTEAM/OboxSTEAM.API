using System.Security.Claims;
using Microsoft.Extensions.Configuration;
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

    public ParentService(IUnitOfWork unitOfWork, IEmailService emailService, IClaimsService claimsService)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _claimsService = claimsService;
    }

    public async Task<bool> RequestParentLinkAsync(RequestLinkDto dto, IConfiguration configuration)
    {
        var studentId = _claimsService.GetCurrentUserId;
        if (studentId == Guid.Empty) throw ErrorHelper.Unauthorized("Unauthorized access.");

        var parentEmail = dto.ParentEmail.ToLower();
        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == parentEmail && u.Role == RoleType.Parent);

        if (parent == null)
        {
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

            // Sinh Magic Token vì đây là lần đầu (nhúng kèm StudentId để duyệt)
            var magicToken = JwtUtils.GenerateActionToken(parent.Id, "MagicLink", studentId.ToString(), configuration, TimeSpan.FromHours(24));
            var loginUrl = $"{configuration["APP_BASE_URL"]}/magic-login?token={magicToken}";

            await _emailService.SendMagicLinkEmailAsync(new ActionEmailRequestDto { To = parentEmail, Link = loginUrl });
            return true;
        }

        // ĐÃ TỒN TẠI PARENT -> Kiểm tra liên kết
        var parentStudent = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(ps => ps.ParentId == parent.Id && ps.StudentId == studentId);
        if (parentStudent != null && parentStudent.IsVerified)
        {
            throw ErrorHelper.Conflict("This parent is already associated with you.");
        }
        else if (parentStudent == null)
        {
            // Chưa có liên kết -> Insert pending link
            var parentStudentDto = new ParentStudentCreateDto { ParentId = parent.Id, StudentId = studentId, IsVerified = false };
            var parentStudentEntity = new ParentStudent { ParentId = parentStudentDto.ParentId, StudentId = parentStudentDto.StudentId, IsVerified = parentStudentDto.IsVerified };
            await _unitOfWork.ParentStudents.AddAsync(parentStudentEntity);
            await _unitOfWork.SaveChangesAsync();
        }

        if (string.IsNullOrEmpty(parent.PasswordHash))
        {
            // Tài khoản ngầm (Shadow) -> Yêu cầu đăng nhập Magic Link
            var magicToken = JwtUtils.GenerateActionToken(parent.Id, "MagicLink", studentId.ToString(), configuration, TimeSpan.FromHours(24));
            var loginUrl = $"{configuration["APP_BASE_URL"]}/magic-login?token={magicToken}";

            await _emailService.SendMagicLinkEmailAsync(new ActionEmailRequestDto { To = parentEmail, Link = loginUrl });
        }
        else
        {
            // Phụ huynh đã có tài khoản chuẩn => Gửi ApproveLink
            var approveToken = JwtUtils.GenerateActionToken(parent.Id, "ApproveLink", studentId.ToString(), configuration, TimeSpan.FromHours(24));
            var approveUrl = $"{configuration["APP_BASE_URL"]}/approve-link?token={approveToken}";

            await _emailService.SendApproveLinkEmailAsync(new ActionEmailRequestDto { To = parentEmail, Link = approveUrl });
        }

        return true;
    }

    public async Task<LoginResponseDto> MagicLoginAsync(MagicLoginDto dto, IConfiguration configuration)
    {
        var principal = JwtUtils.ValidateActionToken(dto.Token, configuration);
        if (principal.FindFirst("Purpose")?.Value != "MagicLink") throw ErrorHelper.BadRequest("Invalid token for login.");

        var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("nameid")?.Value ?? principal.FindFirst("sub")?.Value;
        var studentIdStr = principal.FindFirst("ExtraData")?.Value;

        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parentId))
            throw ErrorHelper.BadRequest("Token does not contain User information.");

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == parentId && !u.IsDeleted);
        if (parent == null) throw ErrorHelper.NotFound("Account does not exist.");
        if (parent.Status == AccountStatus.Locked) throw ErrorHelper.Forbidden("Account has been locked.");

        // Bật cờ xác nhận liên kết cho bé thứ 1
        if (!string.IsNullOrEmpty(studentIdStr) && Guid.TryParse(studentIdStr, out var studentId))
        {
            var ps = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(x => x.ParentId == parent.Id && x.StudentId == studentId);
            if (ps != null && !ps.IsVerified)
            {
                ps.IsVerified = true;
                await _unitOfWork.ParentStudents.Update(ps);
            }
        }
        await _unitOfWork.SaveChangesAsync();

        return await IssueTokensAsync(parent, configuration);
    }

    public async Task<bool> CompleteProfileAsync(CompleteProfileDto dto)
    {
        var parentId = _claimsService.GetCurrentUserId;
        if (parentId == Guid.Empty) throw ErrorHelper.Unauthorized("Unauthorized access.");

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == parentId && !u.IsDeleted);
        if (parent == null) throw ErrorHelper.NotFound("Account does not exist.");

        parent.FullName = dto.FullName;
        parent.Phone = dto.Phone;
        parent.PasswordHash = new PasswordHasher().HashPassword(dto.Password);
        parent.IsEmailVerified = true;
        
        await _unitOfWork.Users.Update(parent);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    public async Task<bool> ApproveLinkAsync(ApproveLinkDto dto, IConfiguration configuration)
    {
        var currentUserId = _claimsService.GetCurrentUserId;
        if (currentUserId == Guid.Empty) throw ErrorHelper.Unauthorized("Unauthorized access.");

        var principal = JwtUtils.ValidateActionToken(dto.Token, configuration);
        if (principal.FindFirst("Purpose")?.Value != "ApproveLink") throw ErrorHelper.BadRequest("Invalid token.");

        var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("nameid")?.Value ?? principal.FindFirst("sub")?.Value;
        var studentIdStr = principal.FindFirst("ExtraData")?.Value;
        
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parentId))
            throw ErrorHelper.BadRequest("Invalid token format.");

        if (currentUserId != parentId)
            throw ErrorHelper.Forbidden("You do not have permission to approve this association.");

        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == parentId && !u.IsDeleted);
        if (parent == null) throw ErrorHelper.NotFound("Account does not exist.");

        // Bật cờ xác nhận cho link
        if (!string.IsNullOrEmpty(studentIdStr) && Guid.TryParse(studentIdStr, out var studentId))
        {
            var ps = await _unitOfWork.ParentStudents.FirstOrDefaultAsync(x => x.ParentId == parent.Id && x.StudentId == studentId);
            if (ps != null && !ps.IsVerified)
            {
                ps.IsVerified = true;
                await _unitOfWork.ParentStudents.Update(ps);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        return true;
    }

    private async Task<LoginResponseDto> IssueTokensAsync(User user, IConfiguration configuration)
    {
        var accessToken = JwtUtils.GenerateJwtToken(user.Id, user.Email, user.Role.ToString(), configuration, TimeSpan.FromMinutes(30));
        var refreshToken = TokenTools.GenerateRefreshToken();
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }
}
