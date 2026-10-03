using Microsoft.Extensions.Logging;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    internal const string CapstoneDriverStudentCode = "STD-CAP-01";
    internal const string CapstoneTruongStudentCode = "STD-CAP-02";
    internal const string CapstoneLongStudentCode = "STD-CAP-03";
    internal const string CapstoneHoaStudentCode = "STD-CAP-04";

    /// <summary>
    /// Mentors only the Smart City class (Flow 2) and requests a board class live (Flow 5).
    /// </summary>
    internal const string CapstoneDemoMentorCode = "MNT-010";

    private static readonly (string Code, string Email, string FullName, string Phone)[] CapstoneStudentAccounts =
    [
        (CapstoneDriverStudentCode, "vietanh@oboxsteam.com", "Đỗ Hữu Việt Anh", "0901000001"),
        (CapstoneTruongStudentCode, "truong@oboxsteam.com", "Nguyễn Nhật Trường", "0901000002"),
        (CapstoneLongStudentCode, "long@oboxsteam.com", "Lê Quang Long", "0901000003"),
        (CapstoneHoaStudentCode, "hoa@oboxsteam.com", "Thanh Hòa", "0901000004"),
    ];

    /// <summary>
    /// Capstone demo students (password Student@123). No avatar or face data: those are created live.
    /// </summary>
    private async Task EnsureCapstoneStudentUsersAsync()
    {
        var usersToAdd = new List<User>();
        foreach (var account in CapstoneStudentAccounts)
        {
            var exists = await _unitOfWork.Users.FirstOrDefaultAsync(
                u => u.Code == account.Code || u.Email == account.Email);
            if (exists != null)
            {
                continue;
            }

            usersToAdd.Add(new User
            {
                Id = Guid.NewGuid(),
                Code = account.Code,
                Email = account.Email,
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = account.FullName,
                Phone = account.Phone,
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (usersToAdd.Count == 0)
        {
            return;
        }

        await _unitOfWork.Users.AddRangeAsync(usersToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Seeded {Count} capstone demo student(s).", usersToAdd.Count);
    }
}
