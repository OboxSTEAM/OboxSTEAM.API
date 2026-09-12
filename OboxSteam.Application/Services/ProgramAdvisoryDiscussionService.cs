using System.Text.Json;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ProgramAdvisoryDiscussionService : IProgramAdvisoryDiscussionService
{
    private const int DefaultPageSize = 30;
    private const int MaxPageSize = 100;
    private const int MaxMessageLength = 10_000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly IAdvisoryReferenceResolver _referenceResolver;

    public ProgramAdvisoryDiscussionService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        IAdvisoryReferenceResolver referenceResolver)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _referenceResolver = referenceResolver;
    }

    public async Task<AdvisoryDiscussionPageDto> GetMessagesAsync(
        Guid programId,
        string? before,
        string? after,
        int pageSize)
    {
        await RequireDiscussionAccessAsync(programId);
        var beforeSequence = ParseCursor(programId, before);
        var afterSequence = ParseCursor(programId, after);
        ValidatePaging(beforeSequence, afterSequence, pageSize);

        var rows = await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
            m => m.ProgramId == programId && !m.IsDeleted);
        var ordered = rows.OrderBy(m => m.Sequence).ThenBy(m => m.CreatedAt).ToList();
        var selected = SelectPage(ordered, beforeSequence, afterSequence, pageSize, out var hasMoreBefore, out var hasMoreAfter);
        var messages = await MapMessagesAsync(selected);

        return new AdvisoryDiscussionPageDto
        {
            Messages = messages,
            Before = messages.Count == 0 ? before : CreateCursor(programId, messages[0].Sequence),
            After = messages.Count == 0 ? after : CreateCursor(programId, messages[^1].Sequence),
            HasMoreBefore = hasMoreBefore,
            HasMoreAfter = hasMoreAfter,
        };
    }

    public async Task<AdvisoryDiscussionMessageDto> GetMessageAsync(Guid programId, Guid messageId)
    {
        await RequireDiscussionAccessAsync(programId);
        var message = await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetByIdAsync(messageId);
        if (message == null || message.IsDeleted || message.ProgramId != programId)
        {
            throw ErrorHelper.NotFound($"Discussion message '{messageId}' was not found.");
        }

        return (await MapMessagesAsync([message])).Single();
    }

    public async Task<AdvisoryDiscussionMessageDto> AddMessageAsync(
        Guid programId,
        PostAdvisoryDiscussionMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = NormalizeText(request.Text);
        var clientMessageId = request.ClientMessageId?.Trim();
        if (text == null)
        {
            throw ErrorHelper.BadRequest("Discussion message text is required.");
        }

        if (text.Length > MaxMessageLength)
        {
            throw ErrorHelper.BadRequest($"Discussion message must be at most {MaxMessageLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(clientMessageId) || clientMessageId.Length > 100)
        {
            throw ErrorHelper.BadRequest("ClientMessageId is required and must be at most 100 characters.");
        }

        var referenceIds = request.ReferenceIds ?? [];
        if (referenceIds.Count > 10)
        {
            throw ErrorHelper.BadRequest("A discussion message may contain at most 10 references.");
        }

        var actorId = _claimsService.GetCurrentUserId;
        return await _unitOfWork.ExecuteAdvisoryTransactionAsync(programId, async () =>
        {
            var (program, actor) = await RequireDiscussionAccessAsync(programId);
            var existing = await _unitOfWork.ProgramAdvisoryDiscussionMessages.FirstOrDefaultAsync(
                m => m.ProgramId == programId
                     && m.AuthorUserId == actorId
                     && m.ClientMessageId == clientMessageId
                     && !m.IsDeleted);
            if (existing != null)
            {
                return await GetMessageAsync(programId, existing.Id);
            }

            var references = await LoadReferenceEntitiesAsync(programId, referenceIds);
            var currentMax = program.AdvisoryDiscussionSequence;
            var existingMax = (await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
                    m => m.ProgramId == programId && !m.IsDeleted))
                .Select(m => m.Sequence)
                .DefaultIfEmpty(0)
                .Max();
            var sequence = Math.Max(currentMax, existingMax) + 1;
            var now = _currentTime.GetCurrentTime().ToUniversalTime();
            var message = new ProgramAdvisoryDiscussionMessage
            {
                Id = Guid.NewGuid(),
                ProgramId = programId,
                AuthorUserId = actor.Id,
                Sequence = sequence,
                Text = text,
                ClientMessageId = clientMessageId,
                CreatedAt = now,
                CreatedBy = actor.Id,
            };

            await _unitOfWork.ProgramAdvisoryDiscussionMessages.AddAsync(message);
            for (var index = 0; index < references.Count; index++)
            {
                await _unitOfWork.ProgramAdvisoryDiscussionMessageReferences.AddAsync(
                    new ProgramAdvisoryDiscussionMessageReference
                    {
                        Id = Guid.NewGuid(),
                        MessageId = message.Id,
                        ReferenceId = references[index].Id,
                        Ordinal = index,
                        CreatedAt = now,
                        CreatedBy = actor.Id,
                    });
            }

            program.AdvisoryDiscussionSequence = sequence;
            await _unitOfWork.Programs.Update(program);
            await _unitOfWork.ProgramAdvisoryNotificationIntents.AddAsync(
                new ProgramAdvisoryNotificationIntent
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programId,
                    EventId = message.Id,
                    EventType = "AdvisoryDiscussionMessageCreated",
                    NotificationType = NotificationType.AdvisoryReply,
                    PayloadJson = JsonSerializer.Serialize(new { programId, messageId = message.Id }),
                    Status = AdvisoryNotificationIntentStatus.Pending,
                    NextAttemptAt = now,
                    CreatedAt = now,
                    CreatedBy = actor.Id,
                });
            await _unitOfWork.SaveChangesAsync();
            return new AdvisoryDiscussionMessageDto
            {
                Id = message.Id,
                ProgramId = message.ProgramId,
                AuthorUserId = actor.Id,
                AuthorName = DisplayName(actor),
                Sequence = message.Sequence,
                Cursor = CreateCursor(programId, message.Sequence),
                Text = message.Text,
                ClientMessageId = message.ClientMessageId,
                CreatedAt = message.CreatedAt,
                References = (await _referenceResolver.ResolveManyAsync(programId, referenceIds)).ToList(),
            };
        });
    }

    public async Task RecordThreadReadAsync(
        Guid programId,
        Guid threadId,
        RecordAdvisoryThreadReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sequence = ParseCursor(programId, request.Cursor) ?? request.LastDisplayedSequence;
        if (sequence < 0)
        {
            throw ErrorHelper.BadRequest("LastDisplayedSequence cannot be negative.");
        }

        await _unitOfWork.ExecuteAdvisoryTransactionAsync(programId, async () =>
        {
            var (_, actor) = await RequireDiscussionAccessAsync(programId);
            var thread = await _unitOfWork.ProgramAdvisoryThreads.GetByIdAsync(threadId);
            if (thread == null || thread.IsDeleted || thread.ProgramId != programId)
            {
                throw ErrorHelper.NotFound($"Advisory thread '{threadId}' was not found.");
            }

            if (sequence > thread.LatestActivitySequence)
            {
                throw ErrorHelper.BadRequest("The supplied thread cursor is not valid for this program.");
            }

            await AdvanceReadAsync(programId, actor.Id, AdvisoryStreamType.Thread, threadId, sequence);
            return true;
        });
    }

    public async Task RecordDiscussionReadAsync(
        Guid programId,
        RecordAdvisoryDiscussionReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sequence = ParseCursor(programId, request.Cursor) ?? request.LastDisplayedSequence;
        if (sequence < 0)
        {
            throw ErrorHelper.BadRequest("LastDisplayedSequence cannot be negative.");
        }

        await _unitOfWork.ExecuteAdvisoryTransactionAsync(programId, async () =>
        {
            var (program, actor) = await RequireDiscussionAccessAsync(programId);
            var currentMax = Math.Max(
                program.AdvisoryDiscussionSequence,
                (await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
                    m => m.ProgramId == programId && !m.IsDeleted))
                .Select(m => m.Sequence)
                .DefaultIfEmpty(0)
                .Max());
            if (sequence > currentMax)
            {
                throw ErrorHelper.BadRequest("The supplied discussion cursor is not valid for this program.");
            }

            await AdvanceReadAsync(programId, actor.Id, AdvisoryStreamType.Discussion, null, sequence);
            return true;
        });
    }

    private async Task<(Program Program, User Actor)> RequireDiscussionAccessAsync(Guid programId)
    {
        var actor = await ResolveActorAsync();
        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        if (actor.Role is RoleType.Manager or RoleType.Admin)
        {
            return (program, actor);
        }

        if (actor.Role != RoleType.Expert)
        {
            throw ErrorHelper.Forbidden("You are not a member of this program advisory team.");
        }

        var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.UserId == actor.Id && !e.IsDeleted);
        if (expert == null)
        {
            throw ErrorHelper.Forbidden("You are not a member of this program advisory team.");
        }

        if (program.AdvisorExpertId == expert.Id)
        {
            return (program, actor);
        }

        var boardMember = await _unitOfWork.ProgramBoards.FirstOrDefaultAsync(
            b => b.ProgramId == programId && b.ExpertId == expert.Id && !b.IsDeleted);
        if (boardMember == null)
        {
            throw ErrorHelper.Forbidden("You are not a member of this program advisory team.");
        }

        return (program, actor);
    }

    private async Task<User> ResolveActorAsync()
    {
        var id = _claimsService.GetCurrentUserId;
        if (id == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Authenticated user id is unavailable.");
        }

        var actor = await _unitOfWork.Users.GetByIdAsync(id);
        if (actor == null || actor.IsDeleted)
        {
            throw ErrorHelper.Unauthorized("Authenticated user was not found.");
        }

        return actor;
    }

    private async Task<List<ProgramAdvisoryReference>> LoadReferenceEntitiesAsync(
        Guid programId,
        IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var distinct = ids.Distinct().ToList();
        if (distinct.Count != ids.Count || distinct.Any(id => id == Guid.Empty))
        {
            throw ErrorHelper.BadRequest("Reference ids must be unique and non-empty.");
        }

        var references = await _unitOfWork.ProgramAdvisoryReferences.GetAllAsync(
            r => distinct.Contains(r.Id) && r.ProgramId == programId && !r.IsDeleted);
        if (references.Count != distinct.Count)
        {
            throw ErrorHelper.BadRequest("All discussion references must belong to this program.");
        }

        return distinct
            .Select(id => references.Single(r => r.Id == id))
            .ToList();
    }

    private async Task<List<AdvisoryDiscussionMessageDto>> MapMessagesAsync(
        IReadOnlyList<ProgramAdvisoryDiscussionMessage> messages)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var authors = await _unitOfWork.Users.GetAllAsync(
            u => messages.Select(m => m.AuthorUserId).Contains(u.Id));
        var references = await _unitOfWork.ProgramAdvisoryDiscussionMessageReferences.GetAllAsync(
            r => messages.Select(m => m.Id).Contains(r.MessageId) && !r.IsDeleted);
        var referenceIds = references.Select(r => r.ReferenceId).Distinct().ToList();
        var referenceDtos = new Dictionary<Guid, AdvisoryReferenceDto>();
        foreach (var referenceId in referenceIds)
        {
            var reference = await _referenceResolver.ResolveAsync(messages[0].ProgramId, referenceId);
            referenceDtos[referenceId] = reference;
        }

        return messages
            .OrderBy(m => m.Sequence)
            .ThenBy(m => m.CreatedAt)
            .Select(message => new AdvisoryDiscussionMessageDto
            {
                Id = message.Id,
                ProgramId = message.ProgramId,
                AuthorUserId = message.AuthorUserId,
                AuthorName = authors.FirstOrDefault(a => a.Id == message.AuthorUserId) is { } author
                    ? DisplayName(author)
                    : null,
                Sequence = message.Sequence,
                Text = message.Text,
                ClientMessageId = message.ClientMessageId,
                CreatedAt = message.CreatedAt,
                References = references
                    .Where(r => r.MessageId == message.Id)
                    .OrderBy(r => r.Ordinal)
                    .Select(r => referenceDtos[r.ReferenceId])
                    .ToList(),
            })
            .ToList();
    }

    private async Task AdvanceReadAsync(
        Guid programId,
        Guid userId,
        AdvisoryStreamType streamType,
        Guid? threadId,
        long sequence)
    {
        var existing = await _unitOfWork.ProgramAdvisoryStreamReads.FirstOrDefaultAsync(
            r => r.ProgramId == programId
                 && r.UserId == userId
                 && r.StreamType == streamType
                 && r.ThreadId == threadId
                 && !r.IsDeleted);
        if (existing == null)
        {
            var now = _currentTime.GetCurrentTime().ToUniversalTime();
            await _unitOfWork.ProgramAdvisoryStreamReads.AddAsync(new ProgramAdvisoryStreamRead
            {
                Id = Guid.NewGuid(),
                ProgramId = programId,
                UserId = userId,
                StreamType = streamType,
                ThreadId = threadId,
                LastReadSequence = sequence,
                CreatedAt = now,
                CreatedBy = userId,
            });
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        if (sequence <= existing.LastReadSequence)
        {
            return;
        }

        existing.LastReadSequence = sequence;
        await _unitOfWork.ProgramAdvisoryStreamReads.Update(existing);
        await _unitOfWork.SaveChangesAsync();
    }

    private static List<ProgramAdvisoryDiscussionMessage> SelectPage(
        List<ProgramAdvisoryDiscussionMessage> ordered,
        long? before,
        long? after,
        int pageSize,
        out bool hasMoreBefore,
        out bool hasMoreAfter)
    {
        if (before.HasValue)
        {
            var candidates = ordered.Where(m => m.Sequence < before.Value).ToList();
            hasMoreBefore = candidates.Count > pageSize;
            hasMoreAfter = ordered.Any(m => m.Sequence >= before.Value);
            return candidates.TakeLast(pageSize).ToList();
        }

        if (after.HasValue)
        {
            var candidates = ordered.Where(m => m.Sequence > after.Value).ToList();
            hasMoreBefore = ordered.Any(m => m.Sequence <= after.Value);
            hasMoreAfter = candidates.Count > pageSize;
            return candidates.Take(pageSize).ToList();
        }

        hasMoreBefore = ordered.Count > pageSize;
        hasMoreAfter = false;
        return ordered.TakeLast(pageSize).ToList();
    }

    private static void ValidatePaging(long? before, long? after, int pageSize)
    {
        if (before.HasValue && after.HasValue)
        {
            throw ErrorHelper.BadRequest("before and after cursors cannot be used together.");
        }

        if (before is < 0 || after is < 0)
        {
            throw ErrorHelper.BadRequest("Discussion cursors cannot be negative.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw ErrorHelper.BadRequest($"Page size must be between 1 and {MaxPageSize}.");
        }
    }

    private static string? NormalizeText(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string CreateCursor(Guid programId, long sequence)
        => $"{programId:D}:{sequence}";

    private static long? ParseCursor(Guid programId, string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        var trimmed = cursor.Trim();
        var separator = trimmed.LastIndexOf(':');
        if (separator > 0)
        {
            var programPart = trimmed[..separator];
            var sequencePart = trimmed[(separator + 1)..];
            if (!Guid.TryParse(programPart, out var cursorProgramId)
                || cursorProgramId != programId
                || !long.TryParse(sequencePart, out var sequence))
            {
                throw ErrorHelper.BadRequest("The supplied advisory cursor is invalid for this program.");
            }

            return sequence;
        }

        if (long.TryParse(trimmed, out var legacySequence))
        {
            return legacySequence;
        }

        throw ErrorHelper.BadRequest("The supplied advisory cursor is invalid.");
    }

    private static string DisplayName(User user)
        => !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName! : user.Email;
}
