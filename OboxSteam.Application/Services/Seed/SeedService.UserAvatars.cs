using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Placeholder portraits for seeded accounts so every list, card and roster shows a face.
/// Seeded URLs are not face-indexed; capstone students upload real photos live (Flow 3).
/// </summary>
public partial class SeedService
{
    private const string SeedPortraitBaseUrl = "https://randomuser.me/api/portraits/";

    private static string MalePortrait(int index) => $"{SeedPortraitBaseUrl}men/{index}.jpg";

    private static string FemalePortrait(int index) => $"{SeedPortraitBaseUrl}women/{index}.jpg";

    internal static readonly (string UserCode, string AvatarUrl)[] SeedUserAvatarPlan =
    [
        ("MNG-001", MalePortrait(13)),
        ("PRT-001", FemalePortrait(14)),

        ("MNT-001", MalePortrait(31)),
        ("MNT-002", FemalePortrait(44)),
        ("MNT-003", MalePortrait(79)),
        ("MNT-004", FemalePortrait(49)),
        ("MNT-005", MalePortrait(88)),
        ("MNT-006", FemalePortrait(59)),
        ("MNT-007", MalePortrait(41)),
        ("MNT-008", MalePortrait(81)),
        ("MNT-009", FemalePortrait(53)),
        (CapstoneDemoMentorCode, MalePortrait(61)),

        ("STD-001", MalePortrait(32)),
        ("STD-002", MalePortrait(14)),
        ("STD-003", FemalePortrait(2)),
        ("STD-004", MalePortrait(22)),
        ("STD-005", FemalePortrait(3)),
        ("STD-006", MalePortrait(43)),
        ("STD-007", FemalePortrait(31)),
        ("STD-008", MalePortrait(46)),
        ("STD-009", FemalePortrait(33)),
        ("STD-010", MalePortrait(62)),
        ("STD-011", FemalePortrait(51)),
        ("STD-012", MalePortrait(60)),
        ("STD-013", FemalePortrait(43)),
        ("STD-014", MalePortrait(84)),
        ("STD-015", MalePortrait(4)),
        ("STD-016", FemalePortrait(17)),
        ("STD-017", MalePortrait(26)),
        ("STD-018", FemalePortrait(27)),
        ("STD-019", MalePortrait(48)),
        ("STD-020", FemalePortrait(48)),
        ("STD-021", MalePortrait(56)),
        ("STD-022", MalePortrait(67)),
        ("STD-023", FemalePortrait(78)),
        ("STD-024", MalePortrait(69)),
        ("STD-025", FemalePortrait(80)),
        ("STD-026", MalePortrait(90)),
        ("STD-027", FemalePortrait(85)),
        ("STD-028", MalePortrait(36)),
        ("STD-029", FemalePortrait(90)),
        ("STD-030", MalePortrait(45)),
        ("STD-031", MalePortrait(47)),
        ("STD-032", FemalePortrait(8)),
        ("STD-033", MalePortrait(85)),
        ("STD-034", FemalePortrait(12)),
        ("STD-035", MalePortrait(86)),
        ("STD-036", FemalePortrait(19)),
        ("STD-037", MalePortrait(89)),
        ("STD-038", MalePortrait(94)),
        ("STD-039", FemalePortrait(29)),
        ("STD-040", MalePortrait(96)),

        ("STD-RV01", FemalePortrait(40)),
        ("STD-RV02", MalePortrait(1)),
        ("STD-RV03", FemalePortrait(26)),
        ("STD-RV04", MalePortrait(7)),
        ("STD-RV05", FemalePortrait(42)),
        ("STD-RV06", MalePortrait(18)),
        ("STD-RV07", FemalePortrait(47)),
        ("STD-RV08", MalePortrait(27)),
        ("STD-RV09", FemalePortrait(56)),
        ("STD-RV10", MalePortrait(38)),
        ("STD-RV11", FemalePortrait(63)),
        ("STD-RV12", MalePortrait(42)),
    ];

    /// <summary>
    /// Fills missing avatars only, so a live upload is never overwritten. Expert login accounts
    /// mirror their public expert card photo.
    /// </summary>
    private async Task SeedUserAvatarsAsync()
    {
        var seedExpertAvatars = SeedExpertAccounts
            .Where(a => !string.IsNullOrWhiteSpace(a.AvatarUrl))
            .ToDictionary(a => a.ExpertCode, a => a.AvatarUrl!, StringComparer.OrdinalIgnoreCase);
        var experts = await _unitOfWork.Experts.GetAllAsync(e => !e.IsDeleted);
        var expertAvatarByUserId = new Dictionary<Guid, string>();
        var expertsUpdated = 0;
        foreach (var expert in experts)
        {
            if ((string.IsNullOrWhiteSpace(expert.AvatarUrl) || expert.AvatarUrl.Contains("placeholder.local"))
                && seedExpertAvatars.TryGetValue(expert.Code, out var seedAvatar))
            {
                expert.AvatarUrl = seedAvatar;
                expert.UpdatedAt = _seedNow;
                expert.UpdatedBy = Guid.Empty;
                await _unitOfWork.Experts.Update(expert);
                expertsUpdated++;
            }

            if (expert.UserId.HasValue && !string.IsNullOrWhiteSpace(expert.AvatarUrl))
            {
                expertAvatarByUserId[expert.UserId.Value] = expert.AvatarUrl;
            }
        }

        var avatarByCode = SeedUserAvatarPlan.ToDictionary(
            p => p.UserCode,
            p => p.AvatarUrl,
            StringComparer.OrdinalIgnoreCase);
        var users = await _unitOfWork.Users.GetAllAsync(
            u => !u.IsDeleted && (u.AvatarUrl == null || u.AvatarUrl == ""));
        var usersUpdated = 0;
        var stillMissing = new List<string>();
        foreach (var user in users)
        {
            var avatarUrl = user.Role == RoleType.Expert
                ? expertAvatarByUserId.GetValueOrDefault(user.Id)
                : avatarByCode.GetValueOrDefault(user.Code);
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                if (user.Role != RoleType.Admin && !IsCapstoneStudentCode(user.Code))
                {
                    stillMissing.Add(user.Code);
                }

                continue;
            }

            user.AvatarUrl = avatarUrl;
            user.UpdatedAt = _seedNow;
            user.UpdatedBy = Guid.Empty;
            await _unitOfWork.Users.Update(user);
            usersUpdated++;
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Seeded avatars: {UserCount} user(s), {ExpertCount} expert card(s).",
            usersUpdated,
            expertsUpdated);
        if (stillMissing.Count > 0)
        {
            _loggerService.LogWarning(
                "Seeded accounts without an avatar: {Codes}",
                string.Join(", ", stillMissing.OrderBy(c => c, StringComparer.OrdinalIgnoreCase)));
        }
    }

    private static bool IsCapstoneStudentCode(string code)
        => CapstoneStudentAccounts.Any(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase));
}
