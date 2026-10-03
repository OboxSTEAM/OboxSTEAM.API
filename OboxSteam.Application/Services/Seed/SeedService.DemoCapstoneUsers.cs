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
    /// Paid students that partly fill the extra Open capstone cohorts (STD-041..058).
    /// Unlike the capstone students they get seeded avatars.
    /// </summary>
    internal static readonly (string Code, string Email, string FullName, string Phone)[] CapstoneOpenClassStudentAccounts =
    [
        ("STD-041", "student41@oboxsteam.com", "Nguyễn Tuấn Anh", "0123456741"),
        ("STD-042", "student42@oboxsteam.com", "Trần Khánh Vy", "0123456742"),
        ("STD-043", "student43@oboxsteam.com", "Lê Quang Vinh", "0123456743"),
        ("STD-044", "student44@oboxsteam.com", "Phạm Thu Trang", "0123456744"),
        ("STD-045", "student45@oboxsteam.com", "Hoàng Minh Tâm", "0123456745"),
        ("STD-046", "student46@oboxsteam.com", "Vũ Ngọc Diệp", "0123456746"),
        ("STD-047", "student47@oboxsteam.com", "Đặng Hải Đăng", "0123456747"),
        ("STD-048", "student48@oboxsteam.com", "Bùi Khánh Huyền", "0123456748"),
        ("STD-049", "student49@oboxsteam.com", "Đỗ Trung Kiên", "0123456749"),
        ("STD-050", "student50@oboxsteam.com", "Ngô Thảo Nguyên", "0123456750"),
        ("STD-051", "student51@oboxsteam.com", "Dương Gia Khang", "0123456751"),
        ("STD-052", "student52@oboxsteam.com", "Lý Mai Anh", "0123456752"),
        ("STD-053", "student53@oboxsteam.com", "Phan Đức Trí", "0123456753"),
        ("STD-054", "student54@oboxsteam.com", "Trịnh Hà My", "0123456754"),
        ("STD-055", "student55@oboxsteam.com", "Mai Thành Đạt", "0123456755"),
        ("STD-056", "student56@oboxsteam.com", "Cao Bảo Trân", "0123456756"),
        ("STD-057", "student57@oboxsteam.com", "Hồ Nhật Quang", "0123456757"),
        ("STD-058", "student58@oboxsteam.com", "Tô Minh Thư", "0123456758"),
    ];

    /// <summary>
    /// Capstone demo students (password Student@123). No avatar or face data: those are created live.
    /// Also creates the Open-cohort filler students.
    /// </summary>
    private async Task EnsureCapstoneStudentUsersAsync()
    {
        var usersToAdd = new List<User>();
        foreach (var account in CapstoneStudentAccounts.Concat(CapstoneOpenClassStudentAccounts))
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
