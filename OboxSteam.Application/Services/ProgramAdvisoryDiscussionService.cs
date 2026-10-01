using System.Text.Json;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Realtime;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ProgramAdvisoryDiscussionService : IProgramAdvisoryDiscussionService
{
    private const int MaxPageSize = 100;
    private const int MaxTextLength = 4000;
    private const int MaxMentions = 20;
    private const int MaxAttachments = 10;
    private const int MaxClientMessageIdLength = 100;
    private const int MaxCapturedLabelLength = 255;
    private const string InvalidStatusCode = "INVALID_STATUS";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly IAdvisoryReferenceResolver _referenceResolver;
    private readonly ISyncEventPublisher _syncEventPublisher;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IAdvisoryPresenceTracker _presenceTracker;

    public ProgramAdvisoryDiscussionService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        IAdvisoryReferenceResolver referenceResolver,
        ISyncEventPublisher syncEventPublisher,
        INotificationPublisher notificationPublisher,
        IAdvisoryPresenceTracker presenceTracker)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _referenceResolver = referenceResolver;
        _syncEventPublisher = syncEventPublisher;
        _notificationPublisher = notificationPublisher;
        _presenceTracker = presenceTracker;
    }

    public async Task<IReadOnlyList<MentionTargetDto>> GetMentionTargetsAsync(Guid programId)
    {
        await RequireParticipantAsync(programId);
        var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, programId);
        return ProgramMentionTargetIndex.Build(tree).Targets;
    }

    public async Task<AdvisoryDiscussionPageDto> GetMessagesAsync(
        Guid programId,
        string? before,
        string? after,
        int pageSize,
        ProgramAdvisoryTargetType? targetType = null,
        Guid? targetId = null)
    {
        await RequireParticipantAsync(programId);
        var beforeSequence = ParseCursor(programId, before);
        var afterSequence = ParseCursor(programId, after);
        ValidatePaging(beforeSequence, afterSequence, pageSize);
        if (targetType.HasValue != targetId.HasValue)
        {
            throw ErrorHelper.BadRequest("targetType and targetId must be supplied together.");
        }

        var rows = await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
            m => m.ProgramId == programId && !m.IsDeleted);
        if (targetType.HasValue)
        {
            var mentioning = await GetMessageIdsMentioningAsync(programId, targetType.Value, targetId!.Value);
            rows = rows.Where(m => m.RemovedAt == null && mentioning.Contains(m.Id)).ToList();
        }

        var ordered = rows.OrderBy(m => m.Sequence).ThenBy(m => m.CreatedAt).ToList();
        var selected = SelectPage(ordered, beforeSequence, afterSequence, pageSize, out var hasMoreBefore, out var hasMoreAfter);
        var messages = await MapMessagesAsync(programId, selected);

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
        await RequireParticipantAsync(programId);
        var message = await RequireMessageAsync(programId, messageId);
        return await MapMessageAsync(message);
    }

    public async Task<AdvisoryDiscussionMessageDto> AddMessageAsync(
        Guid programId,
        PostAdvisoryDiscussionMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = NormalizeText(request.Text);
        var clientMessageId = request.ClientMessageId?.Trim();
        if (string.IsNullOrWhiteSpace(clientMessageId) || clientMessageId.Length > MaxClientMessageIdLength)
        {
            throw ErrorHelper.BadRequest(
                $"ClientMessageId is required and must be at most {MaxClientMessageIdLength} characters.");
        }

        var attachmentIds = request.AttachmentIds ?? [];
        if (attachmentIds.Count > MaxAttachments)
        {
            throw ErrorHelper.BadRequest(
                $"A message may have at most {MaxAttachments} attachments.", "TOO_MANY_ATTACHMENTS");
        }

        if (attachmentIds.Distinct().Count() != attachmentIds.Count || attachmentIds.Any(id => id == Guid.Empty))
        {
            throw ErrorHelper.BadRequest("Attachment ids must be unique and non-empty.", "ATTACHMENT_INVALID");
        }

        var mentions = ValidateText(text, attachmentIds.Count);
        var actorId = _claimsService.GetCurrentUserId;
        var sync = new AdvisorySyncBatch(programId);
        AdvisoryParticipant? author = null;
        ProgramAdvisoryDiscussionMessage? created = null;
        var result = await _unitOfWork.ExecuteAdvisoryTransactionAsync(programId, async () =>
        {
            sync.Reset();
            created = null;
            var participant = await RequireParticipantAsync(programId);
            var existing = await _unitOfWork.ProgramAdvisoryDiscussionMessages.FirstOrDefaultAsync(
                m => m.ProgramId == programId
                     && m.AuthorUserId == actorId
                     && m.ClientMessageId == clientMessageId
                     && !m.IsDeleted);
            if (existing != null)
            {
                return await MapMessageAsync(existing);
            }

            var targets = await ResolveMentionTargetsAsync(programId, mentions);
            var attachments = await LoadUnsentAttachmentsAsync(programId, participant.User.Id, attachmentIds);
            var program = participant.Program;
            var now = Now();
            var message = new ProgramAdvisoryDiscussionMessage
            {
                Id = Guid.NewGuid(),
                ProgramId = programId,
                AuthorUserId = participant.User.Id,
                Kind = DiscussionMessageKind.User,
                Sequence = DiscussionSystemMessageWriter.AllocateSequence(_unitOfWork, program),
                Text = text,
                ClientMessageId = clientMessageId,
                CreatedAt = now,
                CreatedBy = participant.User.Id,
            };

            await _unitOfWork.ProgramAdvisoryDiscussionMessages.AddAsync(message);
            await AddMentionReferencesAsync(message, targets, participant.User.Id, now);
            foreach (var attachment in attachments)
            {
                attachment.MessageId = message.Id;
                attachment.UpdatedAt = now;
                attachment.UpdatedBy = participant.User.Id;
                await _unitOfWork.ProgramAdvisoryDiscussionAttachments.Update(attachment);
            }

            await _unitOfWork.Programs.Update(program);
            await _unitOfWork.SaveChangesAsync();
            sync.DiscussionChanged(message.Sequence);
            author = participant;
            created = message;
            return await MapMessageAsync(message);
        });
        await sync.PublishAsync(_syncEventPublisher);
        if (author != null && created != null)
        {
            await NotifyNewMessageAsync(author, created);
        }

        return result;
    }

    public async Task<AdvisoryDiscussionMessageDto> EditMessageAsync(
        Guid programId,
        Guid messageId,
        EditAdvisoryDiscussionMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = NormalizeText(request.Text);
        var sync = new AdvisorySyncBatch(programId);
        var result = await _unitOfWork.ExecuteAdvisoryTransactionAsync(programId, async () =>
        {
            sync.Reset();
            var participant = await RequireParticipantAsync(programId);
            var message = await RequireMessageAsync(programId, messageId);
            RequireOwnUserMessage(message, participant, "edit");
            if (message.Text == text)
            {
                return await MapMessageAsync(message);
            }

            var attachmentCount = (await _unitOfWork.ProgramAdvisoryDiscussionAttachments.GetAllAsync(
                a => a.MessageId == message.Id && !a.IsDeleted)).Count;
            var mentions = ValidateText(text, attachmentCount);
            var targets = await ResolveMentionTargetsAsync(programId, mentions);
            var now = Now();
            await RemoveMentionReferencesAsync(message.Id);
            await AddMentionReferencesAsync(message, targets, participant.User.Id, now);
            message.Text = text;
            message.EditedAt = now;
            message.UpdatedAt = now;
            message.UpdatedBy = participant.User.Id;
            await _unitOfWork.ProgramAdvisoryDiscussionMessages.Update(message);
            await _unitOfWork.SaveChangesAsync();
            sync.DiscussionChanged(
                DiscussionSystemMessageWriter.LatestSequence(_unitOfWork, participant.Program),
                message.Id);
            return await MapMessageAsync(message);
        });
        await sync.PublishAsync(_syncEventPublisher);
        return result;
    }

    public async Task RemoveMessageAsync(Guid programId, Guid messageId)
    {
        var sync = new AdvisorySyncBatch(programId);
        await _unitOfWork.ExecuteAdvisoryTransactionAsync(programId, async () =>
        {
            sync.Reset();
            var participant = await RequireParticipantAsync(programId);
            var message = await RequireMessageAsync(programId, messageId);
            if (message.Kind == DiscussionMessageKind.System)
            {
                throw ErrorHelper.BadRequest("System messages cannot be deleted.");
            }

            if (message.AuthorUserId != participant.User.Id)
            {
                throw ErrorHelper.Forbidden("Only the author can delete this message.");
            }

            if (message.RemovedAt != null)
            {
                return true;
            }

            var now = Now();
            var wasPinned = message.PinStatus != null;
            await RemoveMentionReferencesAsync(message.Id);
            message.RemovedAt = now;
            message.RemovedByUserId = participant.User.Id;
            message.Text = string.Empty;
            ClearPin(message);
            message.UpdatedAt = now;
            message.UpdatedBy = participant.User.Id;
            await _unitOfWork.ProgramAdvisoryDiscussionMessages.Update(message);
            await _unitOfWork.SaveChangesAsync();
            sync.DiscussionChanged(
                DiscussionSystemMessageWriter.LatestSequence(_unitOfWork, participant.Program),
                message.Id);
            if (wasPinned)
            {
                sync.PinChanged(message.Id);
            }

            return true;
        });
        await sync.PublishAsync(_syncEventPublisher);
    }

    public async Task<AdvisoryDiscussionMessageDto> PinMessageAsync(Guid programId, Guid messageId)
    {
        var sync = new AdvisorySyncBatch(programId);
        AdvisoryParticipant? pinnedBy = null;
        var result = await _unitOfWork.ExecuteAdvisoryTransactionAsync(programId, async () =>
        {
            sync.Reset();
            pinnedBy = null;
            var participant = await RequireParticipantAsync(programId);
            RequireExpertParticipant(participant);
            var message = await RequirePinnableMessageAsync(programId, messageId);
            if (message.PinStatus != null)
            {
                return await MapMessageAsync(message);
            }

            var now = Now();
            ClearPin(message);
            message.PinStatus = DiscussionPinStatus.Open;
            message.PinnedByUserId = participant.User.Id;
            message.PinnedAt = now;
            var saved = await SaveMessageAsync(message, participant.User.Id, now);
            sync.PinChanged(message.Id);
            pinnedBy = participant;
            return saved;
        });
        await sync.PublishAsync(_syncEventPublisher);
        if (pinnedBy != null)
        {
            await _notificationPublisher.PublishAsync(NotificationCatalog.AdvisoryMentionPinned(
                programId,
                messageId,
                pinnedBy.User.Id,
                pinnedBy.Program.Name,
                AdvisoryParticipantAccess.DisplayName(pinnedBy.User)));
        }

        return result;
    }

    public async Task<AdvisoryDiscussionMessageDto> UnpinMessageAsync(Guid programId, Guid messageId)
    {
        var sync = new AdvisorySyncBatch(programId);
        var result = await _unitOfWork.ExecuteAdvisoryTransactionAsync(programId, async () =>
        {
            sync.Reset();
            var participant = await RequireParticipantAsync(programId);
            RequireExpertParticipant(participant);
            var message = await RequirePinnableMessageAsync(programId, messageId);
            if (message.PinStatus == null)
            {
                return await MapMessageAsync(message);
            }

            ClearPin(message);
            var saved = await SaveMessageAsync(message, participant.User.Id, Now());
            sync.PinChanged(message.Id);
            return saved;
        });
        await sync.PublishAsync(_syncEventPublisher);
        return result;
    }

    public async Task<AdvisoryDiscussionMessageDto> PerformPinActionAsync(
        Guid programId,
        Guid messageId,
        AdvisoryDiscussionPinActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Action))
        {
            throw ErrorHelper.BadRequest("Unsupported pin action.");
        }

        var sync = new AdvisorySyncBatch(programId);
        var result = await _unitOfWork.ExecuteAdvisoryTransactionAsync(programId, async () =>
        {
            sync.Reset();
            var participant = await RequireParticipantAsync(programId);
            var message = await RequirePinnableMessageAsync(programId, messageId);
            if (message.PinStatus == null)
            {
                throw ErrorHelper.Conflict("The message is not pinned.", InvalidStatusCode);
            }

            var now = Now();
            switch (request.Action)
            {
                case DiscussionPinAction.MarkAddressed:
                    if (!participant.IsManager)
                    {
                        throw ErrorHelper.Forbidden("Only a manager can mark a pin as addressed.");
                    }

                    RequirePinStatus(message, DiscussionPinStatus.Open);
                    message.PinStatus = DiscussionPinStatus.Addressed;
                    message.AddressedByUserId = participant.User.Id;
                    message.AddressedAt = now;
                    break;
                case DiscussionPinAction.Reopen:
                    RequireExpertParticipant(participant);
                    RequirePinStatus(message, DiscussionPinStatus.Addressed, DiscussionPinStatus.Resolved);
                    message.PinStatus = DiscussionPinStatus.Open;
                    message.AddressedByUserId = null;
                    message.AddressedAt = null;
                    message.ResolvedByUserId = null;
                    message.ResolvedAt = null;
                    break;
                case DiscussionPinAction.Resolve:
                    RequireExpertParticipant(participant);
                    RequirePinStatus(message, DiscussionPinStatus.Open, DiscussionPinStatus.Addressed);
                    message.PinStatus = DiscussionPinStatus.Resolved;
                    message.ResolvedByUserId = participant.User.Id;
                    message.ResolvedAt = now;
                    break;
            }

            var saved = await SaveMessageAsync(message, participant.User.Id, now);
            sync.PinChanged(message.Id);
            return saved;
        });
        await sync.PublishAsync(_syncEventPublisher);
        return result;
    }

    public async Task<IReadOnlyList<AdvisoryDiscussionMessageDto>> GetPinsAsync(
        Guid programId,
        DiscussionPinStatus? status = null)
    {
        await RequireParticipantAsync(programId);
        var pinned = await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
            m => m.ProgramId == programId
                 && !m.IsDeleted
                 && m.RemovedAt == null
                 && m.PinStatus != null
                 && (status == null || m.PinStatus == status));
        var ordered = pinned.OrderBy(m => m.PinnedAt).ThenBy(m => m.Sequence).ToList();
        var mapped = await MapMessagesAsync(programId, ordered);
        return ordered.Select(m => mapped.Single(dto => dto.Id == m.Id)).ToList();
    }

    public async Task<IReadOnlyList<AdvisoryMentionCountDto>> GetMentionCountsAsync(Guid programId)
    {
        await RequireParticipantAsync(programId);
        var messages = await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
            m => m.ProgramId == programId
                 && !m.IsDeleted
                 && m.RemovedAt == null
                 && m.Kind == DiscussionMessageKind.User);
        if (messages.Count == 0)
        {
            return [];
        }

        var messagesById = messages.ToDictionary(m => m.Id);
        var messageIds = messagesById.Keys.ToList();
        var links = await _unitOfWork.ProgramAdvisoryDiscussionMessageReferences.GetAllAsync(
            l => messageIds.Contains(l.MessageId) && !l.IsDeleted);
        var referenceIds = links.Select(l => l.ReferenceId).Distinct().ToList();
        var references = (await _unitOfWork.ProgramAdvisoryReferences.GetAllAsync(
                r => referenceIds.Contains(r.Id) && r.ProgramId == programId && !r.IsDeleted))
            .ToDictionary(r => r.Id);

        return links
            .Where(l => references.ContainsKey(l.ReferenceId))
            .Select(l => (Reference: references[l.ReferenceId], Message: messagesById[l.MessageId]))
            .GroupBy(x => (x.Reference.TargetType, x.Reference.TargetId))
            .Select(g =>
            {
                var distinctMessages = g.Select(x => x.Message).DistinctBy(m => m.Id).ToList();
                return new AdvisoryMentionCountDto
                {
                    TargetType = g.Key.TargetType,
                    TargetId = g.Key.TargetId,
                    MessageCount = distinctMessages.Count,
                    OpenPinCount = distinctMessages.Count(m => m.PinStatus == DiscussionPinStatus.Open),
                };
            })
            .OrderBy(c => c.TargetType)
            .ThenBy(c => c.TargetId)
            .ToList();
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
            var participant = await RequireParticipantAsync(programId);
            var currentMax = Math.Max(
                participant.Program.AdvisoryDiscussionSequence,
                (await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetAllAsync(
                    m => m.ProgramId == programId && !m.IsDeleted))
                .Select(m => m.Sequence)
                .DefaultIfEmpty(0)
                .Max());
            if (sequence > currentMax)
            {
                throw ErrorHelper.BadRequest("The supplied discussion cursor is not valid for this program.");
            }

            await AdvanceReadAsync(programId, participant.User.Id, sequence);
            return true;
        });
    }

    private Task<AdvisoryParticipant> RequireParticipantAsync(Guid programId)
        => AdvisoryParticipantAccess.RequireAsync(_unitOfWork, _claimsService, programId);

    /// <summary>Notifies every other participant who is not in the advisory group right now.</summary>
    private async Task NotifyNewMessageAsync(AdvisoryParticipant author, ProgramAdvisoryDiscussionMessage message)
    {
        var program = author.Program;
        var advisor = program.AdvisorExpertId.HasValue
            ? await _unitOfWork.Experts.GetByIdAsync(program.AdvisorExpertId.Value)
            : null;
        var participants = await AdvisoryParticipantAccess.ListAsync(_unitOfWork, program, advisor);
        var authorName = AdvisoryParticipantAccess.DisplayName(author.User);
        var commands = participants
            .Select(p => p.UserId)
            .Distinct()
            .Where(userId => userId != author.User.Id
                             && !_presenceTracker.IsPresent(program.Id, userId))
            .Select(userId => NotificationCatalog.AdvisoryDiscussionMessage(
                userId,
                program.Id,
                message.Id,
                author.User.Id,
                program.Name,
                authorName))
            .ToList();
        if (commands.Count > 0)
        {
            await _notificationPublisher.PublishManyAsync(commands);
        }
    }

    private async Task<ProgramAdvisoryDiscussionMessage> RequireMessageAsync(Guid programId, Guid messageId)
    {
        var message = await _unitOfWork.ProgramAdvisoryDiscussionMessages.GetByIdAsync(messageId);
        if (message == null || message.IsDeleted || message.ProgramId != programId)
        {
            throw ErrorHelper.NotFound($"Discussion message '{messageId}' was not found.");
        }

        return message;
    }

    private async Task<ProgramAdvisoryDiscussionMessage> RequirePinnableMessageAsync(Guid programId, Guid messageId)
    {
        var message = await RequireMessageAsync(programId, messageId);
        if (message.Kind == DiscussionMessageKind.System)
        {
            throw ErrorHelper.BadRequest("System messages cannot be pinned.");
        }

        if (message.RemovedAt != null)
        {
            throw ErrorHelper.BadRequest("A deleted message cannot be pinned.");
        }

        return message;
    }

    private static void RequireOwnUserMessage(
        ProgramAdvisoryDiscussionMessage message,
        AdvisoryParticipant participant,
        string action)
    {
        if (message.Kind == DiscussionMessageKind.System)
        {
            throw ErrorHelper.BadRequest($"System messages cannot be {action}ed.");
        }

        if (message.AuthorUserId != participant.User.Id)
        {
            throw ErrorHelper.Forbidden($"Only the author can {action} this message.");
        }

        if (message.RemovedAt != null)
        {
            throw ErrorHelper.BadRequest("A deleted message cannot be changed.");
        }
    }

    private static void RequireExpertParticipant(AdvisoryParticipant participant)
    {
        if (!participant.IsExpertParticipant)
        {
            throw ErrorHelper.Forbidden("Only the advisor or a board expert can manage pins.");
        }
    }

    private static void RequirePinStatus(ProgramAdvisoryDiscussionMessage message, params DiscussionPinStatus[] allowed)
    {
        if (!allowed.Contains(message.PinStatus!.Value))
        {
            throw ErrorHelper.Conflict(
                $"This action is not allowed while the pin is {message.PinStatus}.", InvalidStatusCode);
        }
    }

    private static void ClearPin(ProgramAdvisoryDiscussionMessage message)
    {
        message.PinStatus = null;
        message.PinnedByUserId = null;
        message.PinnedAt = null;
        message.AddressedByUserId = null;
        message.AddressedAt = null;
        message.ResolvedByUserId = null;
        message.ResolvedAt = null;
    }

    private async Task<AdvisoryDiscussionMessageDto> SaveMessageAsync(
        ProgramAdvisoryDiscussionMessage message,
        Guid actorId,
        DateTime now)
    {
        message.UpdatedAt = now;
        message.UpdatedBy = actorId;
        await _unitOfWork.ProgramAdvisoryDiscussionMessages.Update(message);
        await _unitOfWork.SaveChangesAsync();
        return await MapMessageAsync(message);
    }

    /// <summary>Validates length, emptiness, and mention count; returns the parsed mentions.</summary>
    private static IReadOnlyList<(ProgramAdvisoryTargetType TargetType, Guid TargetId)> ValidateText(
        string text,
        int attachmentCount)
    {
        if (text.Length > MaxTextLength)
        {
            throw ErrorHelper.BadRequest(
                $"A message must be at most {MaxTextLength} characters.", "MESSAGE_TOO_LONG");
        }

        if (text.Length == 0 && attachmentCount == 0)
        {
            throw ErrorHelper.BadRequest("A message needs text or at least one attachment.", "MESSAGE_EMPTY");
        }

        var mentions = AdvisoryMentionTokens.Parse(text);
        if (mentions.Count > MaxMentions)
        {
            throw ErrorHelper.BadRequest(
                $"A message may mention at most {MaxMentions} components.", "TOO_MANY_MENTIONS");
        }

        return mentions;
    }

    private async Task<List<MentionTargetDto>> ResolveMentionTargetsAsync(
        Guid programId,
        IReadOnlyList<(ProgramAdvisoryTargetType TargetType, Guid TargetId)> mentions)
    {
        if (mentions.Count == 0)
        {
            return [];
        }

        var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, programId);
        var index = ProgramMentionTargetIndex.Build(tree);
        return mentions
            .Select(m => index.Find(m.TargetType, m.TargetId)
                ?? throw ErrorHelper.BadRequest(
                    $"Mentioned {m.TargetType} '{m.TargetId}' does not belong to this program.",
                    "MENTION_TARGET_INVALID"))
            .ToList();
    }

    private async Task AddMentionReferencesAsync(
        ProgramAdvisoryDiscussionMessage message,
        IReadOnlyList<MentionTargetDto> targets,
        Guid actorId,
        DateTime now)
    {
        for (var ordinal = 0; ordinal < targets.Count; ordinal++)
        {
            var target = targets[ordinal];
            var label = target.Label.Length <= MaxCapturedLabelLength
                ? target.Label
                : target.Label[..MaxCapturedLabelLength];
            var reference = new ProgramAdvisoryReference
            {
                Id = Guid.NewGuid(),
                ProgramId = message.ProgramId,
                TargetType = target.TargetType,
                TargetId = target.TargetId,
                AnchorKind = ProgramAdvisoryAnchorKind.Node,
                CapturedLabel = label,
                CapturedExcerpt = label,
                CapturedAt = now,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            await _unitOfWork.ProgramAdvisoryReferences.AddAsync(reference);
            await _unitOfWork.ProgramAdvisoryDiscussionMessageReferences.AddAsync(
                new ProgramAdvisoryDiscussionMessageReference
                {
                    Id = Guid.NewGuid(),
                    MessageId = message.Id,
                    ReferenceId = reference.Id,
                    Ordinal = ordinal,
                    CreatedAt = now,
                    CreatedBy = actorId,
                });
        }
    }

    private async Task RemoveMentionReferencesAsync(Guid messageId)
    {
        var links = await _unitOfWork.ProgramAdvisoryDiscussionMessageReferences.GetAllAsync(
            l => l.MessageId == messageId && !l.IsDeleted);
        if (links.Count > 0)
        {
            await _unitOfWork.ProgramAdvisoryDiscussionMessageReferences.SoftRemoveRange(links);
        }
    }

    private async Task<List<ProgramAdvisoryDiscussionAttachment>> LoadUnsentAttachmentsAsync(
        Guid programId,
        Guid uploaderUserId,
        IReadOnlyList<Guid> attachmentIds)
    {
        if (attachmentIds.Count == 0)
        {
            return [];
        }

        var attachments = await _unitOfWork.ProgramAdvisoryDiscussionAttachments.GetAllAsync(
            a => attachmentIds.Contains(a.Id)
                 && a.ProgramId == programId
                 && a.UploaderUserId == uploaderUserId
                 && a.MessageId == null
                 && !a.IsDeleted);
        if (attachments.Count != attachmentIds.Count)
        {
            throw ErrorHelper.BadRequest(
                "Attachments must be unsent uploads of yours for this program.", "ATTACHMENT_INVALID");
        }

        return attachmentIds.Select(id => attachments.Single(a => a.Id == id)).ToList();
    }

    private async Task<HashSet<Guid>> GetMessageIdsMentioningAsync(
        Guid programId,
        ProgramAdvisoryTargetType targetType,
        Guid targetId)
    {
        var referenceIds = (await _unitOfWork.ProgramAdvisoryReferences.GetAllAsync(
                r => r.ProgramId == programId
                     && r.TargetType == targetType
                     && r.TargetId == targetId
                     && !r.IsDeleted))
            .Select(r => r.Id)
            .ToList();
        if (referenceIds.Count == 0)
        {
            return [];
        }

        return (await _unitOfWork.ProgramAdvisoryDiscussionMessageReferences.GetAllAsync(
                l => referenceIds.Contains(l.ReferenceId) && !l.IsDeleted))
            .Select(l => l.MessageId)
            .ToHashSet();
    }

    private async Task<AdvisoryDiscussionMessageDto> MapMessageAsync(ProgramAdvisoryDiscussionMessage message)
        => (await MapMessagesAsync(message.ProgramId, [message])).Single();

    private async Task<List<AdvisoryDiscussionMessageDto>> MapMessagesAsync(
        Guid programId,
        IReadOnlyList<ProgramAdvisoryDiscussionMessage> messages)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var userIds = messages
            .SelectMany(m => new[] { m.AuthorUserId, m.PinnedByUserId, m.AddressedByUserId, m.ResolvedByUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var users = (await _unitOfWork.Users.GetAllAsync(u => userIds.Contains(u.Id)))
            .ToDictionary(u => u.Id);

        var liveIds = messages.Where(m => m.RemovedAt == null).Select(m => m.Id).ToList();
        List<ProgramAdvisoryDiscussionMessageReference> links = liveIds.Count == 0
            ? []
            : await _unitOfWork.ProgramAdvisoryDiscussionMessageReferences.GetAllAsync(
                l => liveIds.Contains(l.MessageId) && !l.IsDeleted);
        var referenceIds = links.Select(l => l.ReferenceId).Distinct().ToList();
        List<ProgramAdvisoryReference> references = referenceIds.Count == 0
            ? []
            : await _unitOfWork.ProgramAdvisoryReferences.GetAllAsync(r => referenceIds.Contains(r.Id));
        var referenceDtos = await _referenceResolver.ResolveLoadedAsync(programId, references);
        List<ProgramAdvisoryDiscussionAttachment> attachments = liveIds.Count == 0
            ? []
            : await _unitOfWork.ProgramAdvisoryDiscussionAttachments.GetAllAsync(
                a => a.MessageId.HasValue && liveIds.Contains(a.MessageId.Value) && !a.IsDeleted);

        return messages
            .OrderBy(m => m.Sequence)
            .ThenBy(m => m.CreatedAt)
            .Select(message => MapMessage(message, users, links, referenceDtos, attachments))
            .ToList();
    }

    private static AdvisoryDiscussionMessageDto MapMessage(
        ProgramAdvisoryDiscussionMessage message,
        IReadOnlyDictionary<Guid, User> users,
        IReadOnlyList<ProgramAdvisoryDiscussionMessageReference> links,
        IReadOnlyDictionary<Guid, AdvisoryReferenceDto> referenceDtos,
        IReadOnlyList<ProgramAdvisoryDiscussionAttachment> attachments)
    {
        var author = message.AuthorUserId.HasValue ? users.GetValueOrDefault(message.AuthorUserId.Value) : null;
        var dto = new AdvisoryDiscussionMessageDto
        {
            Id = message.Id,
            ProgramId = message.ProgramId,
            Sequence = message.Sequence,
            Cursor = CreateCursor(message.ProgramId, message.Sequence),
            Kind = message.Kind,
            AuthorUserId = message.AuthorUserId,
            AuthorName = author == null ? null : AdvisoryParticipantAccess.DisplayName(author),
            AuthorRole = author?.Role,
            Text = message.Text,
            ClientMessageId = message.ClientMessageId,
            SystemEvent = message.SystemEventCode.HasValue
                ? new AdvisoryDiscussionSystemEventDto
                {
                    Code = message.SystemEventCode.Value,
                    Payload = ParsePayload(message.SystemEventPayloadJson),
                }
                : null,
            CreatedAt = message.CreatedAt,
            EditedAt = message.EditedAt,
            IsDeleted = message.RemovedAt != null,
        };

        if (dto.IsDeleted)
        {
            dto.Text = string.Empty;
            return dto;
        }

        dto.References = links
            .Where(l => l.MessageId == message.Id && referenceDtos.ContainsKey(l.ReferenceId))
            .OrderBy(l => l.Ordinal)
            .Select(l => referenceDtos[l.ReferenceId])
            .ToList();
        dto.Attachments = attachments
            .Where(a => a.MessageId == message.Id)
            .OrderBy(a => a.CreatedAt)
            .Select(MapAttachment)
            .ToList();
        dto.Pin = message.PinStatus.HasValue
            ? new AdvisoryDiscussionPinDto
            {
                Status = message.PinStatus.Value,
                PinnedByUserId = message.PinnedByUserId,
                PinnedByName = NameOf(users, message.PinnedByUserId),
                PinnedAt = message.PinnedAt,
                AddressedByUserId = message.AddressedByUserId,
                AddressedByName = NameOf(users, message.AddressedByUserId),
                AddressedAt = message.AddressedAt,
                ResolvedByUserId = message.ResolvedByUserId,
                ResolvedByName = NameOf(users, message.ResolvedByUserId),
                ResolvedAt = message.ResolvedAt,
            }
            : null;
        return dto;
    }

    internal static AdvisoryDiscussionAttachmentDto MapAttachment(ProgramAdvisoryDiscussionAttachment attachment)
        => new()
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            Kind = attachment.Kind,
            UploaderUserId = attachment.UploaderUserId,
            CreatedAt = attachment.CreatedAt,
        };

    private static string? NameOf(IReadOnlyDictionary<Guid, User> users, Guid? userId)
        => userId.HasValue && users.TryGetValue(userId.Value, out var user)
            ? AdvisoryParticipantAccess.DisplayName(user)
            : null;

    private static JsonElement? ParsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private async Task AdvanceReadAsync(Guid programId, Guid userId, long sequence)
    {
        var existing = await _unitOfWork.ProgramAdvisoryStreamReads.FirstOrDefaultAsync(
            r => r.ProgramId == programId
                 && r.UserId == userId
                 && !r.IsDeleted);
        if (existing == null)
        {
            var now = Now();
            await _unitOfWork.ProgramAdvisoryStreamReads.AddAsync(new ProgramAdvisoryStreamRead
            {
                Id = Guid.NewGuid(),
                ProgramId = programId,
                UserId = userId,
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

    private DateTime Now() => _currentTime.GetCurrentTime().ToUniversalTime();

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

    private static string NormalizeText(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

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
}
