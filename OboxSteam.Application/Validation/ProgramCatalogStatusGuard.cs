using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Catalog lifecycle on create/update. Approved is not writable through program CRUD —
/// use the approval endpoints and publish.
/// </summary>
public static class ProgramCatalogStatusGuard
{
    public static void EnsureCreateIsDraft(ProgramStatus? status)
    {
        if (status.HasValue && status.Value != ProgramStatus.Draft)
        {
            throw ErrorHelper.BadRequest(
                "New programs must be created as Draft. Use the approval flow and publish to change status.");
        }
    }

    /// <summary>
    /// Applies an optional catalog toggle. Returns true when status changed.
    /// </summary>
    public static bool ApplyUpdate(Program program, ProgramStatus? requested)
    {
        if (!requested.HasValue || requested.Value == program.Status)
        {
            return false;
        }

        var current = program.Status;
        var next = requested.Value;
        var isCatalogToggle =
            (current == ProgramStatus.Active || current == ProgramStatus.Inactive)
            && (next == ProgramStatus.Active || next == ProgramStatus.Inactive);

        if (!isCatalogToggle)
        {
            throw ErrorHelper.BadRequest(
                "Program status cannot be changed via update. Use the approval endpoints or publish.");
        }

        program.Status = next;
        return true;
    }
}
