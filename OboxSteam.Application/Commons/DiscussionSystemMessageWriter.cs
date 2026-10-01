using System.Text.Json;
using OboxSteam.Application.Commons.CurriculumChanges;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Allocates discussion sequences and adds system messages. Callers persist the program
/// (its <see cref="Program.AdvisoryDiscussionSequence"/> is advanced here) and save.
/// </summary>
public static class DiscussionSystemMessageWriter
{
    public static long AllocateSequence(IUnitOfWork unitOfWork, Program program)
    {
        var sequence = LatestSequence(unitOfWork, program) + 1;
        program.AdvisoryDiscussionSequence = sequence;
        return sequence;
    }

    /// <summary>Highest sequence currently in the program chat (0 when empty).</summary>
    public static long LatestSequence(IUnitOfWork unitOfWork, Program program)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(program);
        var existingMax = unitOfWork.ProgramAdvisoryDiscussionMessages
            .GetQueryable()
            .Where(m => m.ProgramId == program.Id)
            .Select(m => (long?)m.Sequence)
            .Max() ?? 0;
        return Math.Max(program.AdvisoryDiscussionSequence, existingMax);
    }

    public static async Task<ProgramAdvisoryDiscussionMessage> AddAsync(
        IUnitOfWork unitOfWork,
        Program program,
        DiscussionSystemEventCode code,
        object payload,
        DateTime now,
        Guid createdBy)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var message = new ProgramAdvisoryDiscussionMessage
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            AuthorUserId = null,
            Kind = DiscussionMessageKind.System,
            Sequence = AllocateSequence(unitOfWork, program),
            Text = string.Empty,
            ClientMessageId = $"system:{Guid.NewGuid():N}",
            SystemEventCode = code,
            SystemEventPayloadJson = JsonSerializer.Serialize(payload, CurriculumChangeJson.Options),
            CreatedAt = now,
            CreatedBy = createdBy,
        };
        await unitOfWork.ProgramAdvisoryDiscussionMessages.AddAsync(message);
        return message;
    }
}
