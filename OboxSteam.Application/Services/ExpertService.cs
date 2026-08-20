using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ExpertDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class ExpertService : IExpertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;
    private readonly ILogger<ExpertService> _logger;

    public ExpertService(IUnitOfWork unitOfWork, IBlobService blobService, ILogger<ExpertService> logger)
    {
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _logger = logger;
    }

    /// <summary>Treats empty GUID as null — external experts do not require a system user link.</summary>
    private static Guid? NormalizeOptionalUserId(Guid? userId) =>
        userId is { } id && id != Guid.Empty ? id : null;


    public async Task<ExpertProgramSummaryDto> UpdateProgramOfExpertAsync(Guid expertId, Guid programId)
    {
        _logger.LogInformation("[UpdateProgramOfExpertAsync] Updating program {ProgramId} for expert {ExpertId}.", programId, expertId);

        var expert = await _unitOfWork.Experts.GetByIdAsync(expertId);
        if (expert == null || expert.IsDeleted)
        {
            _logger.LogWarning("[UpdateProgramOfExpertAsync] Expert with Id {ExpertId} not found.", expertId);
            throw ErrorHelper.NotFound($"Expert with id '{expertId}' not found.");
        }

        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            _logger.LogWarning("[UpdateProgramOfExpertAsync] Program with Id {ProgramId} not found.", programId);
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        var programBoard = await _unitOfWork.ProgramBoards
            .FirstOrDefaultAsync(pb => pb.ExpertId == expertId && pb.ProgramId == programId);

        if (programBoard == null)
        {
            _logger.LogWarning("[UpdateProgramOfExpertAsync] Program {ProgramId} is not assigned to expert {ExpertId}.", programId, expertId);
            throw ErrorHelper.NotFound($"Program '{programId}' is not assigned to expert '{expertId}'.");
        }

        var updateDto = new ExpertProgramAssignmentDto
        {
            ProgramId = programId,
            RoleInBoard = programBoard.RoleInBoard
        };

        var isUpdated = UpdateHelper.ApplyUpdates(programBoard, updateDto);

        if (isUpdated)
        {
            await _unitOfWork.ProgramBoards.Update(programBoard);
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("[UpdateProgramOfExpertAsync] Program {ProgramId} updated for expert {ExpertId}.", programId, expertId);

        return new ExpertProgramSummaryDto
        {
            ProgramId = program.Id,
            Code = program.Code,
            Name = program.Name,
            RoleInBoard = programBoard.RoleInBoard
        };
    }

    public async Task<ExpertResponseDto> GetExpertByIdAsync(Guid id)
    {
        _logger.LogInformation("[GetExpertByIdAsync] Fetching expert with Id: {Id}", id);

        var expert = await _unitOfWork.Experts.GetByIdAsync(id, e => e.ProgramBoards);

        if (expert == null || expert.IsDeleted)
        {
            _logger.LogWarning("[GetExpertByIdAsync] Expert with Id {Id} not found.", id);
            throw ErrorHelper.NotFound($"Expert with id '{id}' not found.");
        }

        var programIds = expert.ProgramBoards.Select(pb => pb.ProgramId).ToList();
        var programs = programIds.Any()
            ? await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id))
            : new List<Program>();

        var programsById = programs.ToDictionary(p => p.Id, p => p);

        var response = new ExpertResponseDto
        {
            Id = expert.Id,
            Code = expert.Code,
            UserId = expert.UserId,
            FullName = expert.FullName,
            Title = expert.Title,
            Organization = expert.Organization,
            Bio = expert.Bio,
            AvatarUrl = expert.AvatarUrl,
            LinkedInUrl = expert.LinkedInUrl,
            Achievements = expert.Achievements,
            Specialization = expert.Specialization ?? [],
            CreatedAt = expert.CreatedAt,
            UpdatedAt = expert.UpdatedAt,
            Programs = expert.ProgramBoards
                .Where(pb => programsById.ContainsKey(pb.ProgramId))
                .Select(pb => new ExpertProgramSummaryDto
                {
                    ProgramId = pb.ProgramId,
                    Code = programsById[pb.ProgramId].Code,
                    Name = programsById[pb.ProgramId].Name,
                    RoleInBoard = pb.RoleInBoard
                })
                .ToList(),
            Degrees = (await _unitOfWork.ExpertDegrees.GetAllAsync(d => d.ExpertId == expert.Id && !d.IsDeleted))
                .OrderByDescending(d => d.Year)
                .ThenBy(d => d.Title)
                .Select(MapDegree)
                .ToList(),
            Publications = (await _unitOfWork.ExpertPublications.GetAllAsync(p => p.ExpertId == expert.Id && !p.IsDeleted))
                .OrderByDescending(p => p.Year)
                .ThenBy(p => p.Title)
                .Select(MapPublication)
                .ToList()
        };

        _logger.LogInformation("[GetExpertByIdAsync] Expert with Id {Id} retrieved successfully.", id);
        return response;
    }

    public async Task<Pagination<ExpertResponseDto>> GetAllExpertsAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code = null)
    {
        _logger.LogInformation("[GetAllExpertsAsync] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
            page, pageSize, search);

        var query = _unitOfWork.Experts
            .GetQueryable()
            .Where(e => !e.IsDeleted);
        _logger.LogInformation("[GetAllExpertsAsync] Base query initialized.");

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(e =>
                e.FullName.ToLower().Contains(lowerSearch) ||
                e.Code.ToLower().Contains(lowerSearch));
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            query = query.Where(e => e.Code.ToLower().Contains(code.ToLower()));
        }

        query = sortBy?.ToLower() switch
        {
            "fullname" => isDescending ? query.OrderByDescending(e => e.FullName) : query.OrderBy(e => e.FullName),
            "code" => isDescending ? query.OrderByDescending(e => e.Code) : query.OrderBy(e => e.Code),
            "createdat" => isDescending ? query.OrderByDescending(e => e.CreatedAt) : query.OrderBy(e => e.CreatedAt),
            _ => isDescending ? query.OrderByDescending(e => e.CreatedAt) : query.OrderBy(e => e.CreatedAt),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var expertIds = items.Select(e => e.Id).ToList();
        var programBoards = await _unitOfWork.Repository<ProgramBoard>().GetAllAsync(pb => expertIds.Contains(pb.ExpertId));
        var programIds = programBoards.Select(pb => pb.ProgramId).Distinct().ToList();
        var programs = programIds.Any()
            ? await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id))
            : new List<Program>();

        var degrees = expertIds.Count == 0
            ? new List<ExpertDegree>()
            : await _unitOfWork.ExpertDegrees.GetAllAsync(d => expertIds.Contains(d.ExpertId) && !d.IsDeleted);
        var publications = expertIds.Count == 0
            ? new List<ExpertPublication>()
            : await _unitOfWork.ExpertPublications.GetAllAsync(p => expertIds.Contains(p.ExpertId) && !p.IsDeleted);

        var programsById = programs.ToDictionary(p => p.Id, p => p);
        var programBoardsByExpert = programBoards
            .GroupBy(pb => pb.ExpertId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var degreesByExpert = degrees
            .GroupBy(d => d.ExpertId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var publicationsByExpert = publications
            .GroupBy(p => p.ExpertId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dtos = items.Select(expert => new ExpertResponseDto
        {
            Id = expert.Id,
            Code = expert.Code,
            UserId = expert.UserId,
            FullName = expert.FullName,
            Title = expert.Title,
            Organization = expert.Organization,
            Bio = expert.Bio,
            AvatarUrl = expert.AvatarUrl,
            LinkedInUrl = expert.LinkedInUrl,
            Achievements = expert.Achievements,
            Specialization = expert.Specialization ?? [],
            CreatedAt = expert.CreatedAt,
            UpdatedAt = expert.UpdatedAt,
            Programs = programBoardsByExpert.TryGetValue(expert.Id, out var expertBoards)
                ? expertBoards
                    .Where(pb => programsById.ContainsKey(pb.ProgramId))
                    .Select(pb => new ExpertProgramSummaryDto
                    {
                        ProgramId = pb.ProgramId,
                        Code = programsById[pb.ProgramId].Code,
                        Name = programsById[pb.ProgramId].Name,
                        RoleInBoard = pb.RoleInBoard
                    })
                    .ToList()
                : new List<ExpertProgramSummaryDto>(),
            Degrees = degreesByExpert.TryGetValue(expert.Id, out var expertDegrees)
                ? expertDegrees.OrderByDescending(d => d.Year).ThenBy(d => d.Title).Select(MapDegree).ToList()
                : [],
            Publications = publicationsByExpert.TryGetValue(expert.Id, out var expertPublications)
                ? expertPublications.OrderByDescending(p => p.Year).ThenBy(p => p.Title).Select(MapPublication).ToList()
                : []
        }).ToList();

        _logger.LogInformation("[GetAllExpertsAsync] Retrieved {Count}/{Total} experts.", dtos.Count, totalCount);

        return new Pagination<ExpertResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ExpertResponseDto> AddExpertAsync(ExpertCreateDto expertCreateDto)
    {
        var userId = NormalizeOptionalUserId(expertCreateDto.UserId);

        _logger.LogInformation("[AddExpertAsync] Start adding expert: {Name} (Code: {Code}, LinkedUser: {UserId})",
            expertCreateDto.FullName, expertCreateDto.Code, userId);

        var existing = await _unitOfWork.Experts.FirstOrDefaultAsync(
            e => e.Code.ToLower() == expertCreateDto.Code.ToLower() && !e.IsDeleted);

        if (existing != null)
        {
            _logger.LogWarning("[AddExpertAsync] Expert with code '{Code}' already exists.", expertCreateDto.Code);
            throw ErrorHelper.Conflict($"Expert with code '{expertCreateDto.Code}' already exists.");
        }

        if (userId.HasValue)
        {
            var existingUserExpert = await _unitOfWork.Experts.FirstOrDefaultAsync(
                e => e.UserId == userId && !e.IsDeleted);

            if (existingUserExpert != null)
            {
                _logger.LogWarning("[AddExpertAsync] User '{UserId}' already linked to an expert.", userId);
                throw ErrorHelper.Conflict($"User '{userId}' is already linked to an expert.");
            }

            var userExists = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (userExists == null)
            {
                _logger.LogWarning("[AddExpertAsync] User '{UserId}' not found.", userId);
                throw ErrorHelper.NotFound($"User with id '{userId}' not found.");
            }
        }

        var distinctAssignments = (expertCreateDto.Programs ?? [])
            .Where(a => a.ProgramId != Guid.Empty)
            .GroupBy(a => a.ProgramId)
            .Select(g => g.First())
            .ToList();

        if (distinctAssignments.Any())
        {
            var programIds = distinctAssignments.Select(a => a.ProgramId).ToList();
            var programs = await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id));

            if (programs.Count != programIds.Count)
            {
                var existingIds = programs.Select(p => p.Id).ToHashSet();
                var missingId = programIds.First(pid => !existingIds.Contains(pid));
                _logger.LogWarning("[AddExpertAsync] Program '{ProgramId}' not found.", missingId);
                throw ErrorHelper.NotFound($"Program with id '{missingId}' not found.");
            }
        }

        var expert = new Expert
        {
            Id = Guid.NewGuid(),
            Code = expertCreateDto.Code,
            UserId = userId,
            FullName = expertCreateDto.FullName,
            Title = expertCreateDto.Title,
            Organization = expertCreateDto.Organization,
            Bio = expertCreateDto.Bio,
            AvatarUrl = expertCreateDto.AvatarUrl,
            LinkedInUrl = expertCreateDto.LinkedInUrl,
            Achievements = expertCreateDto.Achievements,
            Specialization = expertCreateDto.Specialization ?? []
        };

        await _unitOfWork.Experts.AddAsync(expert);
        await _unitOfWork.SaveChangesAsync();

        if (distinctAssignments.Any())
        {
            var programBoards = distinctAssignments.Select(assignment => new ProgramBoard
            {
                Id = Guid.NewGuid(),
                ExpertId = expert.Id,
                ProgramId = assignment.ProgramId,
                RoleInBoard = assignment.RoleInBoard
            }).ToList();

            await _unitOfWork.ProgramBoards.AddRangeAsync(programBoards);
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("[AddExpertAsync] Expert '{Code}' added successfully with Id {Id}.",
            expert.Code, expert.Id);

        return await GetExpertByIdAsync(expert.Id);
    }

    /// <summary>
    /// Upload avatar for an expert.
    /// Deletes the old avatar (if any) before uploading the new one.
    /// </summary>
    public async Task<ExpertResponseDto> UploadAvatarAsync(Guid id, IFormFile file)
    {
        _logger.LogInformation("[UploadAvatarAsync] Uploading avatar for expert ID: {ExpertId}", id);

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            throw ErrorHelper.BadRequest("Only image files (.jpg, .jpeg, .png, .gif) are allowed.");

        if (file.Length > 5 * 1024 * 1024)
            throw ErrorHelper.BadRequest("Avatar file size must not exceed 5 MB.");

        var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        if (expert == null)
            throw ErrorHelper.NotFound($"Expert with id '{id}' not found.");

        if (!string.IsNullOrWhiteSpace(expert.AvatarUrl))
        {
            _logger.LogInformation("[UploadAvatarAsync] Deleting old avatar for expert {ExpertId}", id);
            await _blobService.DeleteFileAsync(expert.AvatarUrl);
        }

        var fileName = $"{id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream, "avatars");

        var avatarUrl = await _blobService.GetPreviewUrlAsync($"avatars/{fileName}");
        expert.AvatarUrl = avatarUrl;

        await _unitOfWork.Experts.Update(expert);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[UploadAvatarAsync] Avatar uploaded successfully for expert ID: {ExpertId}", id);

        return await GetExpertByIdAsync(expert.Id);
    }

    public async Task<ExpertProgramSummaryDto> AddProgramToExpertAsync(Guid expertId, Guid programId, AddProgramToExpertDto? dto = null)
    {
        _logger.LogInformation("[AddProgramToExpertAsync] Adding program {ProgramId} to expert {ExpertId}.", programId, expertId);

        var expert = await _unitOfWork.Experts.GetByIdAsync(expertId);
        if (expert == null || expert.IsDeleted)
        {
            _logger.LogWarning("[AddProgramToExpertAsync] Expert with Id {ExpertId} not found.", expertId);
            throw ErrorHelper.NotFound($"Expert with id '{expertId}' not found.");
        }

        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            _logger.LogWarning("[AddProgramToExpertAsync] Program with Id {ProgramId} not found.", programId);
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        var existingBoard = await _unitOfWork.ProgramBoards
            .FirstOrDefaultAsync(pb => pb.ExpertId == expertId && pb.ProgramId == programId);

        if (existingBoard != null)
        {
            _logger.LogWarning("[AddProgramToExpertAsync] Program {ProgramId} already assigned to expert {ExpertId}.", programId, expertId);
            throw ErrorHelper.Conflict($"Program '{programId}' is already assigned to expert '{expertId}'.");
        }

        var programBoard = new ProgramBoard
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            ProgramId = programId,
            RoleInBoard = dto?.RoleInBoard
        };

        await _unitOfWork.ProgramBoards.AddAsync(programBoard);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[AddProgramToExpertAsync] Program {ProgramId} added to expert {ExpertId}.", programId, expertId);

        return new ExpertProgramSummaryDto
        {
            ProgramId = program.Id,
            Code = program.Code,
            Name = program.Name,
            RoleInBoard = programBoard.RoleInBoard
        };
    }

    public async Task<ExpertResponseDto> UpdateExpertAsync(Guid id, ExpertUpdateDto expertUpdateDto)
    {
        _logger.LogInformation("[UpdateExpertAsync] Attempting to update expert with Id: {Id}", id);

        var expert = await _unitOfWork.Experts.GetByIdAsync(id, e => e.ProgramBoards);

        if (expert == null || expert.IsDeleted)
        {
            _logger.LogWarning("[UpdateExpertAsync] Expert with Id {Id} not found.", id);
            throw ErrorHelper.NotFound($"Expert with id '{id}' not found.");
        }

        if (!string.IsNullOrWhiteSpace(expertUpdateDto.Code) &&
            !expert.Code.Equals(expertUpdateDto.Code, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _unitOfWork.Experts.FirstOrDefaultAsync(
                e => e.Code.ToLower() == expertUpdateDto.Code.ToLower() &&
                     !e.IsDeleted &&
                     e.Id != id);

            if (duplicate != null)
            {
                _logger.LogWarning("[UpdateExpertAsync] Code '{Code}' is already in use.", expertUpdateDto.Code);
                throw ErrorHelper.Conflict($"Expert with code '{expertUpdateDto.Code}' already exists.");
            }
        }

        var normalizedUserId = NormalizeOptionalUserId(expertUpdateDto.UserId);

        if (normalizedUserId.HasValue && expert.UserId != normalizedUserId)
        {
            var existingUserExpert = await _unitOfWork.Experts.FirstOrDefaultAsync(
                e => e.UserId == normalizedUserId && !e.IsDeleted);

            if (existingUserExpert != null && existingUserExpert.Id != id)
            {
                _logger.LogWarning("[UpdateExpertAsync] User '{UserId}' already linked to an expert.", normalizedUserId);
                throw ErrorHelper.Conflict($"User '{normalizedUserId}' is already linked to an expert.");
            }

            var userExists = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == normalizedUserId);
            if (userExists == null)
            {
                _logger.LogWarning("[UpdateExpertAsync] User '{UserId}' not found.", normalizedUserId);
                throw ErrorHelper.NotFound($"User with id '{normalizedUserId}' not found.");
            }
        }

        expertUpdateDto.UserId = normalizedUserId;

        var isUpdated = UpdateHelper.ApplyUpdates(expert, expertUpdateDto);

        if (expertUpdateDto.Programs != null)
        {
            var distinctAssignments = expertUpdateDto.Programs
                .GroupBy(a => a.ProgramId)
                .Select(g => g.First())
                .ToList();

            if (distinctAssignments.Any())
            {
                var programIds = distinctAssignments.Select(a => a.ProgramId).ToList();
                var programs = await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id));

                if (programs.Count != programIds.Count)
                {
                    var existingIds = programs.Select(p => p.Id).ToHashSet();
                    var missingId = programIds.First(pid => !existingIds.Contains(pid));
                    throw ErrorHelper.NotFound($"Program with id '{missingId}' not found.");
                }
            }

            var existingBoards = await _unitOfWork.ProgramBoards.GetAllAsync(pb => pb.ExpertId == expert.Id);

            if (existingBoards.Any())
            {
                await _unitOfWork.ProgramBoards.HardRemoveRange(existingBoards);
                await _unitOfWork.SaveChangesAsync();
            }

            if (distinctAssignments.Any())
            {
                var programBoards = distinctAssignments.Select(assignment => new ProgramBoard
                {
                    Id = Guid.NewGuid(),
                    ExpertId = expert.Id,
                    ProgramId = assignment.ProgramId,
                    RoleInBoard = assignment.RoleInBoard
                }).ToList();

                await _unitOfWork.ProgramBoards.AddRangeAsync(programBoards);
                await _unitOfWork.SaveChangesAsync();
            }

            isUpdated = true;
        }

        if (!isUpdated)
        {
            _logger.LogWarning("[UpdateExpertAsync] No changes detected for expert Id: {Id}", id);
            return await GetExpertByIdAsync(id);
        }

        await _unitOfWork.Experts.Update(expert);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[UpdateExpertAsync] Expert Id {Id} updated successfully.", id);

        return await GetExpertByIdAsync(expert.Id);
    }

    public async Task<bool> RemoveProgramFromExpertAsync(Guid expertId, Guid programId)
    {
        _logger.LogInformation("[RemoveProgramFromExpertAsync] Removing program {ProgramId} from expert {ExpertId}.", programId, expertId);

        var expert = await _unitOfWork.Experts.GetByIdAsync(expertId);
        if (expert == null || expert.IsDeleted)
        {
            _logger.LogWarning("[RemoveProgramFromExpertAsync] Expert with Id {ExpertId} not found.", expertId);
            throw ErrorHelper.NotFound($"Expert with id '{expertId}' not found.");
        }

        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
        {
            _logger.LogWarning("[RemoveProgramFromExpertAsync] Program with Id {ProgramId} not found.", programId);
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        var programBoard = await _unitOfWork.ProgramBoards
            .FirstOrDefaultAsync(pb => pb.ExpertId == expertId && pb.ProgramId == programId);

        if (programBoard == null)
        {
            _logger.LogWarning("[RemoveProgramFromExpertAsync] Program {ProgramId} is not assigned to expert {ExpertId}.", programId, expertId);
            throw ErrorHelper.NotFound($"Program '{programId}' is not assigned to expert '{expertId}'.");
        }

        await _unitOfWork.ProgramBoards.HardRemoveRange(new List<ProgramBoard> { programBoard });
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[RemoveProgramFromExpertAsync] Program {ProgramId} removed from expert {ExpertId}.", programId, expertId);

        return true;
    }

    public async Task<bool> DeleteExpertAsync(Guid id)
    {
        _logger.LogInformation("[DeleteExpertAsync] Attempting to soft-delete expert Id: {Id}", id);

        var expert = await _unitOfWork.Experts.GetByIdAsync(id);

        if (expert == null || expert.IsDeleted)
        {
            _logger.LogWarning("[DeleteExpertAsync] Expert with Id {Id} not found.", id);
            throw ErrorHelper.NotFound($"Expert with id '{id}' not found.");
        }

        await _unitOfWork.Experts.SoftRemove(expert);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[DeleteExpertAsync] Expert Id {Id} soft-deleted successfully.", id);

        return true;
    }

    public async Task<ExpertDegreeResponseDto> AddDegreeAsync(Guid expertId, ExpertDegreeRequestDto dto)
    {
        await RequireExpertAsync(expertId);
        ExpertProfileValidator.ValidateDegreeRequest(dto.Title, dto.Institution, dto.Year);

        var degree = new ExpertDegree
        {
            ExpertId = expertId,
            Title = dto.Title.Trim(),
            Institution = dto.Institution.Trim(),
            Year = dto.Year,
        };

        await _unitOfWork.ExpertDegrees.AddAsync(degree);
        await _unitOfWork.SaveChangesAsync();
        return MapDegree(degree);
    }

    public async Task<ExpertDegreeResponseDto> UpdateDegreeAsync(
        Guid expertId,
        Guid degreeId,
        ExpertDegreeRequestDto dto)
    {
        await RequireExpertAsync(expertId);
        ExpertProfileValidator.ValidateDegreeRequest(dto.Title, dto.Institution, dto.Year);

        var degree = await _unitOfWork.ExpertDegrees.GetByIdAsync(degreeId);
        if (degree == null || degree.IsDeleted || degree.ExpertId != expertId)
        {
            throw ErrorHelper.NotFound($"Degree with id '{degreeId}' not found.");
        }

        degree.Title = dto.Title.Trim();
        degree.Institution = dto.Institution.Trim();
        degree.Year = dto.Year;
        await _unitOfWork.ExpertDegrees.Update(degree);
        await _unitOfWork.SaveChangesAsync();
        return MapDegree(degree);
    }

    public async Task<bool> DeleteDegreeAsync(Guid expertId, Guid degreeId)
    {
        await RequireExpertAsync(expertId);

        var degree = await _unitOfWork.ExpertDegrees.GetByIdAsync(degreeId);
        if (degree == null || degree.IsDeleted || degree.ExpertId != expertId)
        {
            throw ErrorHelper.NotFound($"Degree with id '{degreeId}' not found.");
        }

        await _unitOfWork.ExpertDegrees.SoftRemove(degree);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<ExpertPublicationResponseDto> AddPublicationAsync(
        Guid expertId,
        ExpertPublicationRequestDto dto)
    {
        await RequireExpertAsync(expertId);
        ExpertProfileValidator.ValidatePublicationRequest(dto.Title, dto.Year);

        var publication = new ExpertPublication
        {
            ExpertId = expertId,
            Title = dto.Title.Trim(),
            Venue = string.IsNullOrWhiteSpace(dto.Venue) ? null : dto.Venue.Trim(),
            Year = dto.Year,
            Url = string.IsNullOrWhiteSpace(dto.Url) ? null : dto.Url.Trim(),
        };

        await _unitOfWork.ExpertPublications.AddAsync(publication);
        await _unitOfWork.SaveChangesAsync();
        return MapPublication(publication);
    }

    public async Task<ExpertPublicationResponseDto> UpdatePublicationAsync(
        Guid expertId,
        Guid publicationId,
        ExpertPublicationRequestDto dto)
    {
        await RequireExpertAsync(expertId);
        ExpertProfileValidator.ValidatePublicationRequest(dto.Title, dto.Year);

        var publication = await _unitOfWork.ExpertPublications.GetByIdAsync(publicationId);
        if (publication == null || publication.IsDeleted || publication.ExpertId != expertId)
        {
            throw ErrorHelper.NotFound($"Publication with id '{publicationId}' not found.");
        }

        publication.Title = dto.Title.Trim();
        publication.Venue = string.IsNullOrWhiteSpace(dto.Venue) ? null : dto.Venue.Trim();
        publication.Year = dto.Year;
        publication.Url = string.IsNullOrWhiteSpace(dto.Url) ? null : dto.Url.Trim();
        await _unitOfWork.ExpertPublications.Update(publication);
        await _unitOfWork.SaveChangesAsync();
        return MapPublication(publication);
    }

    public async Task<bool> DeletePublicationAsync(Guid expertId, Guid publicationId)
    {
        await RequireExpertAsync(expertId);

        var publication = await _unitOfWork.ExpertPublications.GetByIdAsync(publicationId);
        if (publication == null || publication.IsDeleted || publication.ExpertId != expertId)
        {
            throw ErrorHelper.NotFound($"Publication with id '{publicationId}' not found.");
        }

        await _unitOfWork.ExpertPublications.SoftRemove(publication);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task<Expert> RequireExpertAsync(Guid expertId)
    {
        var expert = await _unitOfWork.Experts.GetByIdAsync(expertId);
        if (expert == null || expert.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Expert with id '{expertId}' not found.");
        }

        return expert;
    }

    private static ExpertDegreeResponseDto MapDegree(ExpertDegree degree) => new()
    {
        Id = degree.Id,
        ExpertId = degree.ExpertId,
        Title = degree.Title,
        Institution = degree.Institution,
        Year = degree.Year,
    };

    private static ExpertPublicationResponseDto MapPublication(ExpertPublication publication) => new()
    {
        Id = publication.Id,
        ExpertId = publication.ExpertId,
        Title = publication.Title,
        Venue = publication.Venue,
        Year = publication.Year,
        Url = publication.Url,
    };
}

