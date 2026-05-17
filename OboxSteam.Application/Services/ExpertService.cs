using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ExpertDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class ExpertService : IExpertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExpertService> _logger;

    public ExpertService(IUnitOfWork unitOfWork, ILogger<ExpertService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }


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

        var programsById = programs.ToDictionary(p => p.Id, p => p);
        var programBoardsByExpert = programBoards
            .GroupBy(pb => pb.ExpertId)
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
                : new List<ExpertProgramSummaryDto>()
        }).ToList();

        _logger.LogInformation("[GetAllExpertsAsync] Retrieved {Count}/{Total} experts.", dtos.Count, totalCount);

        return new Pagination<ExpertResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ExpertResponseDto> AddExpertAsync(ExpertCreateDto expertCreateDto)
    {
        _logger.LogInformation("[AddExpertAsync] Start adding expert: {Name} (Code: {Code})",
            expertCreateDto.FullName, expertCreateDto.Code);

        var existing = await _unitOfWork.Experts.FirstOrDefaultAsync(
            e => e.Code.ToLower() == expertCreateDto.Code.ToLower() && !e.IsDeleted);

        if (existing != null)
        {
            _logger.LogWarning("[AddExpertAsync] Expert with code '{Code}' already exists.", expertCreateDto.Code);
            throw ErrorHelper.Conflict($"Expert with code '{expertCreateDto.Code}' already exists.");
        }

        if (expertCreateDto.UserId.HasValue)
        {
            var existingUserExpert = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.UserId == expertCreateDto.UserId);
            if (existingUserExpert != null)
            {
                _logger.LogWarning("[AddExpertAsync] User '{UserId}' already linked to an expert.", expertCreateDto.UserId);
                throw ErrorHelper.Conflict($"User '{expertCreateDto.UserId}' is already linked to an expert.");
            }

            var userExists = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == expertCreateDto.UserId);
            if (userExists == null)
            {
                _logger.LogWarning("[AddExpertAsync] User '{UserId}' not found.", expertCreateDto.UserId);
                throw ErrorHelper.NotFound($"User with id '{expertCreateDto.UserId}' not found.");
            }
        }

        var expert = new Expert
        {
            Code = expertCreateDto.Code,
            UserId = expertCreateDto.UserId,
            FullName = expertCreateDto.FullName,
            Title = expertCreateDto.Title,
            Organization = expertCreateDto.Organization,
            Bio = expertCreateDto.Bio,
            AvatarUrl = expertCreateDto.AvatarUrl,
            LinkedInUrl = expertCreateDto.LinkedInUrl,
            Achievements = expertCreateDto.Achievements
        };

        await _unitOfWork.Experts.AddAsync(expert);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[AddExpertAsync] Expert '{Code}' added successfully with Id {Id}.",
            expert.Code, expert.Id);

        return await GetExpertByIdAsync(expert.Id);
    }

    public async Task<ExpertProgramSummaryDto> AddProgramToExpertAsync(Guid expertId, Guid programId)
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
            RoleInBoard = null
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

        if (expertUpdateDto.UserId.HasValue && expert.UserId != expertUpdateDto.UserId)
        {
            var existingUserExpert = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.UserId == expertUpdateDto.UserId);
            if (existingUserExpert != null && existingUserExpert.Id != id)
            {
                _logger.LogWarning("[UpdateExpertAsync] User '{UserId}' already linked to an expert.", expertUpdateDto.UserId);
                throw ErrorHelper.Conflict($"User '{expertUpdateDto.UserId}' is already linked to an expert.");
            }

            var userExists = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == expertUpdateDto.UserId);
            if (userExists == null)
            {
                _logger.LogWarning("[UpdateExpertAsync] User '{UserId}' not found.", expertUpdateDto.UserId);
                throw ErrorHelper.NotFound($"User with id '{expertUpdateDto.UserId}' not found.");
            }
        }

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


}
