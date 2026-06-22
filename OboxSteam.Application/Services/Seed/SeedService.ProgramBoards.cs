using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedProgramBoardsAsync()
    {
        _loggerService.LogInformation("Starting seed program boards");
        var existingProgramBoards = await _unitOfWork.ProgramBoards.GetAllAsync();

        if (!existingProgramBoards.Any())
        {
            var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001");
            var expert002 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-002");
            var expert003 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-003");
            var expert004 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-004");
            var expert005 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-005");
            var expert006 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-006");
            var expert007 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-007");

            var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
            var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
            var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");
            var programPyBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-PYBASIC");
            var programMathFun = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-MATHFUN");
            var programDigArt = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-DIGART");
            var programBiotech = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-BIOTECH");
            var programAiBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-AIBASIC");
            var programEnvSci = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ENVSCI");
            var programGameDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-GAMEDEV");
            var programMusicTech = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-MUSICTECH");
            var programDataMath = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-DATAMATH");
            var program3DDesign = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-3DDESIGN");

            var programBoards = new List<ProgramBoard>();
            var seedUtc = DateTime.UtcNow;

            void AddBoard(Expert? expert, Program? program, string role)
            {
                if (expert == null || program == null)
                    return;

                programBoards.Add(new ProgramBoard
                {
                    Id = Guid.NewGuid(),
                    ProgramId = program.Id,
                    ExpertId = expert.Id,
                    RoleInBoard = role,
                    CreatedAt = seedUtc,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            AddBoard(expert001, programRobotics, "Lead Robotics Advisor");
            AddBoard(expert001, programIot, "IoT Hardware Mentor");
            AddBoard(expert002, programRobotics, "STEAM Curriculum Advisor");
            AddBoard(expert002, programBiotech, "Life Sciences Board Member");
            AddBoard(expert003, programWebDev, "Lead Web Development Advisor");
            AddBoard(expert003, programGameDev, "Game Development Mentor");
            AddBoard(expert004, programAiBasic, "AI Program Director");
            AddBoard(expert004, programPyBasic, "Programming Advisor");
            AddBoard(expert004, programDataMath, "Data Science Board Member");
            AddBoard(expert005, programMathFun, "Lead Mathematics Advisor");
            AddBoard(expert005, programDataMath, "Statistics Board Member");
            AddBoard(expert006, programDigArt, "Lead Digital Arts Advisor");
            AddBoard(expert006, program3DDesign, "3D Design Mentor");
            AddBoard(expert006, programMusicTech, "Creative Arts Board Member");
            AddBoard(expert007, programEnvSci, "Environmental Science Director");
            AddBoard(expert007, programBiotech, "Sustainability Advisor");
            AddBoard(expert002, programEnvSci, "STEAM Outreach Advisor");

            if (programBoards.Any())
            {
                await _unitOfWork.ProgramBoards.AddRangeAsync(programBoards);
                await _unitOfWork.SaveChangesAsync();
            }

            _loggerService.LogInformation("Finished seed program boards");
        }
        else
        {
            _loggerService.LogInformation("Program boards already exist, skipping program board seeding");
        }

    }
}

