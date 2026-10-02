using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Commons;

/// <summary>Pinned and latest published framework version numbers of a program.</summary>
public static class FrameworkVersionLookup
{
    public static async Task<FrameworkVersionInfo> ResolveAsync(IUnitOfWork unitOfWork, Program program)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(program);
        if (!program.FrameworkId.HasValue)
        {
            return new FrameworkVersionInfo(null, null);
        }

        var frameworkId = program.FrameworkId.Value;
        var published = await unitOfWork.ProgramFrameworkVersions.GetAllAsync(
            v => v.FrameworkId == frameworkId && v.IsPublished && !v.IsDeleted);
        int? current = published.FirstOrDefault(v => v.Id == program.FrameworkVersionId)?.VersionNumber;
        if (current == null && program.FrameworkVersionId.HasValue)
        {
            current = (await unitOfWork.ProgramFrameworkVersions.GetByIdAsync(program.FrameworkVersionId.Value))?.VersionNumber;
        }

        int? latest = published.Count == 0 ? null : published.Max(v => v.VersionNumber);
        return new FrameworkVersionInfo(current, latest);
    }
}
