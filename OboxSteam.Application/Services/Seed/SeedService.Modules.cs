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
    private async Task SeedModulesAsync()
    {
        _loggerService.LogInformation("Starting seed modules");
        var existingModules = await _unitOfWork.Modules.GetAllAsync();
        if (!existingModules.Any())
        {
            var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
            var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
            var programSteam = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-STEAM-01");
            var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");
            var programPyBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-PYBASIC");
            var programMathFun = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-MATHFUN");
            var programDigArt = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-DIGART");
            var programBiotech = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-BIOTECH");
            var program3DDesign = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-3DDESIGN");
            var programAiBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-AIBASIC");
            var programEnvSci = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ENVSCI");
            var programGameDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-GAMEDEV");
            var programMusicTech = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-MUSICTECH");
            var programDataMath = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-DATAMATH");
            var programCertTest = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-CERT-TEST");

            var modules = new List<Module>();

            if (programRobotics != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-ROBOTICS-01",
                        ProgramId = programRobotics.Id,
                        Name = "Basics of Robotics",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 450_000m,
                        RetakeFee = 100_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-ROBOTICS-02",
                        ProgramId = programRobotics.Id,
                        Name = "Sensors and Movement",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        PrerequisiteModuleId = null,
                        IsMandatory = true,
                        Price = 500_000m,
                        RetakeFee = 120_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-ROBOTICS-03",
                        ProgramId = programRobotics.Id,
                        Name = "Build and Test Challenge",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = true,
                        Price = 550_000m,
                        RetakeFee = 150_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-ROBOTICS not found. Skipping robotics module seeding.");
            }

            if (programWebDev != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-WEBDEV-01",
                        ProgramId = programWebDev.Id,
                        Name = "HTML & CSS Foundations",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 700_000m,
                        RetakeFee = 150_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-WEBDEV-02",
                        ProgramId = programWebDev.Id,
                        Name = "JavaScript Basics",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 800_000m,
                        RetakeFee = 180_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-WEBDEV-03",
                        ProgramId = programWebDev.Id,
                        Name = "Responsive Design & Deployment",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 700_000m,
                        RetakeFee = 150_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-WEBDEV not found. Skipping web development module seeding.");
            }

            if (programSteam != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-STEAM-01",
                        ProgramId = programSteam.Id,
                        Name = "STEAM Lab Kickoff",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 500_000m,
                        RetakeFee = 110_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-STEAM-02",
                        ProgramId = programSteam.Id,
                        Name = "Creative Prototyping",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 550_000m,
                        RetakeFee = 120_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-STEAM-01 not found. Skipping STEAM module seeding.");
            }

            if (programIot != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-IOT-01",
                        ProgramId = programIot.Id,
                        Name = "Sensors and Microcontrollers",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 650_000m,
                        RetakeFee = 130_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-IOT-02",
                        ProgramId = programIot.Id,
                        Name = "Cloud Connectivity Lab",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 700_000m,
                        RetakeFee = 150_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-IOT-03",
                        ProgramId = programIot.Id,
                        Name = "IoT Project Showcase",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 600_000m,
                        RetakeFee = 120_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-IOT not found. Skipping IoT module seeding.");
            }

            // ── PRG-PYBASIC ──────────────────────────────────────────────────
            if (programPyBasic != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-PYBASIC-01",
                        ProgramId = programPyBasic.Id,
                        Name = "Python Syntax & Data Types",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 480_000m,
                        RetakeFee = 100_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-PYBASIC-02",
                        ProgramId = programPyBasic.Id,
                        Name = "Control Flow & Functions",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 500_000m,
                        RetakeFee = 110_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-PYBASIC-03",
                        ProgramId = programPyBasic.Id,
                        Name = "Mini-Project: Python Game",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 470_000m,
                        RetakeFee = 100_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-PYBASIC not found. Skipping Python module seeding.");
            }

            // ── PRG-MATHFUN ──────────────────────────────────────────────────
            if (programMathFun != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-MATHFUN-01",
                        ProgramId = programMathFun.Id,
                        Name = "Algebra & Number Patterns",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 380_000m,
                        RetakeFee = 80_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-MATHFUN-02",
                        ProgramId = programMathFun.Id,
                        Name = "Geometry & Spatial Thinking",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 380_000m,
                        RetakeFee = 80_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-MATHFUN-03",
                        ProgramId = programMathFun.Id,
                        Name = "Math Puzzle Challenge",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 340_000m,
                        RetakeFee = 70_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-MATHFUN not found. Skipping Math module seeding.");
            }

            // ── PRG-DIGART ──────────────────────────────────────────────────
            if (programDigArt != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-DIGART-01",
                        ProgramId = programDigArt.Id,
                        Name = "Color Theory & Digital Tools",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 600_000m,
                        RetakeFee = 130_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-DIGART-02",
                        ProgramId = programDigArt.Id,
                        Name = "Character Design Workshop",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 650_000m,
                        RetakeFee = 140_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-DIGART-03",
                        ProgramId = programDigArt.Id,
                        Name = "Portfolio Illustration Project",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 550_000m,
                        RetakeFee = 110_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-DIGART not found. Skipping Digital Art module seeding.");
            }

            // ── PRG-BIOTECH ──────────────────────────────────────────────────
            if (programBiotech != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-BIOTECH-01",
                        ProgramId = programBiotech.Id,
                        Name = "Cell Biology Fundamentals",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 700_000m,
                        RetakeFee = 150_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-BIOTECH-02",
                        ProgramId = programBiotech.Id,
                        Name = "Genetics & Lab Simulation",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 750_000m,
                        RetakeFee = 160_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-BIOTECH-03",
                        ProgramId = programBiotech.Id,
                        Name = "Biotechnology Case Study",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 600_000m,
                        RetakeFee = 120_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-BIOTECH not found. Skipping Biotech module seeding.");
            }

            // ── PRG-3DDESIGN ─────────────────────────────────────────────────
            if (program3DDesign != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-3DDESIGN-01",
                        ProgramId = program3DDesign.Id,
                        Name = "CAD Basics & Interface",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 800_000m,
                        RetakeFee = 170_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-3DDESIGN-02",
                        ProgramId = program3DDesign.Id,
                        Name = "3D Modeling Practice",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 850_000m,
                        RetakeFee = 180_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-3DDESIGN-03",
                        ProgramId = program3DDesign.Id,
                        Name = "Print & Prototype Challenge",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 650_000m,
                        RetakeFee = 130_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-3DDESIGN not found. Skipping 3D Design module seeding.");
            }

            // ── PRG-AIBASIC ──────────────────────────────────────────────────
            if (programAiBasic != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-AIBASIC-01",
                        ProgramId = programAiBasic.Id,
                        Name = "What is AI? Concepts & History",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 800_000m,
                        RetakeFee = 170_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-AIBASIC-02",
                        ProgramId = programAiBasic.Id,
                        Name = "Image Recognition Hands-On",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 850_000m,
                        RetakeFee = 180_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-AIBASIC-03",
                        ProgramId = programAiBasic.Id,
                        Name = "Build Your Own Chatbot",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 800_000m,
                        RetakeFee = 160_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-AIBASIC not found. Skipping AI module seeding.");
            }

            // ── PRG-ENVSCI ───────────────────────────────────────────────────
            if (programEnvSci != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-ENVSCI-01",
                        ProgramId = programEnvSci.Id,
                        Name = "Ecology & Ecosystems",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 450_000m,
                        RetakeFee = 90_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-ENVSCI-02",
                        ProgramId = programEnvSci.Id,
                        Name = "Climate Change Field Research",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 500_000m,
                        RetakeFee = 100_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-ENVSCI-03",
                        ProgramId = programEnvSci.Id,
                        Name = "Sustainability Action Project",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 400_000m,
                        RetakeFee = 80_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-ENVSCI not found. Skipping Environmental Science module seeding.");
            }

            // ── PRG-GAMEDEV ──────────────────────────────────────────────────
            if (programGameDev != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-GAMEDEV-01",
                        ProgramId = programGameDev.Id,
                        Name = "Game Design Fundamentals",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 900_000m,
                        RetakeFee = 190_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-GAMEDEV-02",
                        ProgramId = programGameDev.Id,
                        Name = "2D Sprite & Level Design",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 950_000m,
                        RetakeFee = 200_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-GAMEDEV-03",
                        ProgramId = programGameDev.Id,
                        Name = "Game Logic & Scripting",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 3,
                        IsMandatory = true,
                        Price = 950_000m,
                        RetakeFee = 200_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-GAMEDEV-04",
                        ProgramId = programGameDev.Id,
                        Name = "Publish Your Game",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 4,
                        IsMandatory = false,
                        Price = 900_000m,
                        RetakeFee = 180_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-GAMEDEV not found. Skipping Game Dev module seeding.");
            }

            // ── PRG-MUSICTECH ────────────────────────────────────────────────
            if (programMusicTech != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-MUSICTECH-01",
                        ProgramId = programMusicTech.Id,
                        Name = "Music Theory Essentials",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 550_000m,
                        RetakeFee = 110_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-MUSICTECH-02",
                        ProgramId = programMusicTech.Id,
                        Name = "DAW Production Workshop",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 600_000m,
                        RetakeFee = 120_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-MUSICTECH-03",
                        ProgramId = programMusicTech.Id,
                        Name = "Original Track Showcase",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 450_000m,
                        RetakeFee = 90_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-MUSICTECH not found. Skipping Music Tech module seeding.");
            }

            // ── PRG-DATAMATH ─────────────────────────────────────────────────
            if (programDataMath != null)
            {
                modules.AddRange(new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-DATAMATH-01",
                        ProgramId = programDataMath.Id,
                        Name = "Statistics & Probability",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 650_000m,
                        RetakeFee = 130_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-DATAMATH-02",
                        ProgramId = programDataMath.Id,
                        Name = "Data Visualization Lab",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 700_000m,
                        RetakeFee = 140_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-DATAMATH-03",
                        ProgramId = programDataMath.Id,
                        Name = "Real-World Data Analysis Project",
                        ModuleType = ModuleType.Research,
                        ModuleOrder = 3,
                        IsMandatory = false,
                        Price = 600_000m,
                        RetakeFee = 120_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty
                    }
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-DATAMATH not found. Skipping Data Math module seeding.");
            }

            if (programCertTest != null)
            {
                modules.Add(new Module
                {
                    Id = Guid.NewGuid(),
                    Code = "MOD-CERT-TEST-01",
                    ProgramId = programCertTest.Id,
                    Name = "Certificate Test Module",
                    ModuleType = ModuleType.Theory,
                    ModuleOrder = 1,
                    IsMandatory = true,
                    Price = 50_000m,
                    RetakeFee = 10_000m,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty
                });
            }
            else
            {
                _loggerService.LogWarning("Program PRG-CERT-TEST not found. Skipping certificate test module seeding.");
            }

            if (modules.Count > 0)
            {
                foreach (var module in modules)
                {
                    if (module.LearningOutcomes.Length > 0)
                    {
                        continue;
                    }

                    module.LearningOutcomes = module.Code switch
                    {
                        "MOD-CERT-TEST-01" => new[]
                        {
                            "Complete a single self-paced reading activity",
                            "Demonstrate program progress completion",
                            "Validate certificate eligibility rules"
                        },
                        "MOD-ROBOTICS-01" => new[]
                        {
                            "Understand core robotics concepts and components",
                            "Identify basic mechanical structures and actuators",
                            "Follow safety practices in a robotics lab"
                        },
                        "MOD-ROBOTICS-02" => new[]
                        {
                            "Work with common sensors and read sensor data",
                            "Implement movement control for simple robots",
                            "Debug and calibrate sensor-based behaviors"
                        },
                        "MOD-ROBOTICS-03" => new[]
                        {
                            "Plan and build a small robot for a challenge",
                            "Test, iterate, and improve performance",
                            "Present results and reflect on engineering trade-offs"
                        },
                        "MOD-WEBDEV-01" => new[]
                        {
                            "Build semantic HTML structures",
                            "Style pages with modern CSS",
                            "Create clean layouts using Flexbox/Grid"
                        },
                        "MOD-WEBDEV-02" => new[]
                        {
                            "Write JavaScript with variables, functions, and control flow",
                            "Manipulate the DOM to create interactivity",
                            "Use debugging tools to fix common issues"
                        },
                        "MOD-WEBDEV-03" => new[]
                        {
                            "Design responsive pages for multiple screen sizes",
                            "Understand deployment basics and hosting",
                            "Ship a small web project end-to-end"
                        },
                        "MOD-STEAM-01" => new[]
                        {
                            "Understand STEAM learning workflow and lab rules",
                            "Explore core tools used in the program",
                            "Practice collaboration and documentation"
                        },
                        "MOD-STEAM-02" => new[]
                        {
                            "Prototype ideas using simple materials and tools",
                            "Iterate quickly based on feedback",
                            "Communicate design choices clearly"
                        },
                        "MOD-IOT-01" => new[]
                        {
                            "Understand microcontrollers and basic electronics",
                            "Read data from sensors in embedded projects",
                            "Build simple circuits safely"
                        },
                        "MOD-IOT-02" => new[]
                        {
                            "Send device data to the cloud",
                            "Understand connectivity basics (Wi‑Fi/API)",
                            "Monitor and troubleshoot IoT data flow"
                        },
                        "MOD-IOT-03" => new[]
                        {
                            "Build a small IoT prototype as a project",
                            "Demonstrate end-to-end device-to-cloud flow",
                            "Present outcomes and lessons learned"
                        },
                        "MOD-PYBASIC-01" => new[]
                        {
                            "Write Python syntax confidently",
                            "Work with core data types and operators",
                            "Practice clean coding and formatting"
                        },
                        "MOD-PYBASIC-02" => new[]
                        {
                            "Use conditions and loops effectively",
                            "Write and reuse functions",
                            "Solve small problems with structured thinking"
                        },
                        "MOD-PYBASIC-03" => new[]
                        {
                            "Apply Python fundamentals in a mini-project",
                            "Plan simple game logic and implement it",
                            "Test and refine features"
                        },
                        "MOD-MATHFUN-01" => new[]
                        {
                            "Build number sense through games",
                            "Practice basic arithmetic strategies",
                            "Develop confidence with math challenges"
                        },
                        "MOD-MATHFUN-02" => new[]
                        {
                            "Recognize patterns and sequences",
                            "Solve puzzles using logical reasoning",
                            "Explain solutions clearly"
                        },
                        "MOD-DIGART-01" => new[]
                        {
                            "Use basic digital drawing tools",
                            "Apply color and composition principles",
                            "Create simple artwork with layers"
                        },
                        "MOD-DIGART-02" => new[]
                        {
                            "Design characters or scenes digitally",
                            "Improve line, shading, and texture",
                            "Export artwork for sharing"
                        },
                        "MOD-BIOTECH-01" => new[]
                        {
                            "Understand basic biology and lab safety",
                            "Observe and document simple experiments",
                            "Learn scientific method basics"
                        },
                        "MOD-3DDESIGN-01" => new[]
                        {
                            "Model simple 3D shapes and objects",
                            "Understand dimensions and constraints",
                            "Prepare models for printing or presentation"
                        },
                        "MOD-AIBASIC-01" => new[]
                        {
                            "Understand what AI is and common applications",
                            "Learn basic ML concepts (data, training, prediction)",
                            "Discuss AI ethics at a beginner level"
                        },
                        _ => new[]
                        {
                            "Understand key concepts of this module",
                            "Practice skills through hands-on activities",
                            "Apply learning in a small project or assessment"
                        }
                    };
                }

                await _unitOfWork.Modules.AddRangeAsync(modules);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed modules — {Count} module(s) created.", modules.Count);
            }
            else
            {
                _loggerService.LogWarning("No modules seeded because required programs were not found.");
            }
        }
        else
        {
            _loggerService.LogInformation("Modules already exist, skipping module seeding");

            var modulesToBackfill = await _unitOfWork.Modules.GetAllAsync(
                m => !m.IsDeleted && (m.LearningOutcomes == null || m.LearningOutcomes.Length == 0));

            if (modulesToBackfill.Any())
            {
                foreach (var module in modulesToBackfill)
                {
                    module.LearningOutcomes = module.Code switch
                    {
                        "MOD-ROBOTICS-01" => new[]
                        {
                            "Understand core robotics concepts and components",
                            "Identify basic mechanical structures and actuators",
                            "Follow safety practices in a robotics lab"
                        },
                        "MOD-ROBOTICS-02" => new[]
                        {
                            "Work with common sensors and read sensor data",
                            "Implement movement control for simple robots",
                            "Debug and calibrate sensor-based behaviors"
                        },
                        "MOD-ROBOTICS-03" => new[]
                        {
                            "Plan and build a small robot for a challenge",
                            "Test, iterate, and improve performance",
                            "Present results and reflect on engineering trade-offs"
                        },
                        "MOD-WEBDEV-01" => new[]
                        {
                            "Build semantic HTML structures",
                            "Style pages with modern CSS",
                            "Create clean layouts using Flexbox/Grid"
                        },
                        "MOD-WEBDEV-02" => new[]
                        {
                            "Write JavaScript with variables, functions, and control flow",
                            "Manipulate the DOM to create interactivity",
                            "Use debugging tools to fix common issues"
                        },
                        "MOD-WEBDEV-03" => new[]
                        {
                            "Design responsive pages for multiple screen sizes",
                            "Understand deployment basics and hosting",
                            "Ship a small web project end-to-end"
                        },
                        "MOD-STEAM-01" => new[]
                        {
                            "Understand STEAM learning workflow and lab rules",
                            "Explore core tools used in the program",
                            "Practice collaboration and documentation"
                        },
                        "MOD-STEAM-02" => new[]
                        {
                            "Prototype ideas using simple materials and tools",
                            "Iterate quickly based on feedback",
                            "Communicate design choices clearly"
                        },
                        "MOD-IOT-01" => new[]
                        {
                            "Understand microcontrollers and basic electronics",
                            "Read data from sensors in embedded projects",
                            "Build simple circuits safely"
                        },
                        "MOD-IOT-02" => new[]
                        {
                            "Send device data to the cloud",
                            "Understand connectivity basics (Wi‑Fi/API)",
                            "Monitor and troubleshoot IoT data flow"
                        },
                        "MOD-IOT-03" => new[]
                        {
                            "Build a small IoT prototype as a project",
                            "Demonstrate end-to-end device-to-cloud flow",
                            "Present outcomes and lessons learned"
                        },
                        "MOD-PYBASIC-01" => new[]
                        {
                            "Write Python syntax confidently",
                            "Work with core data types and operators",
                            "Practice clean coding and formatting"
                        },
                        "MOD-PYBASIC-02" => new[]
                        {
                            "Use conditions and loops effectively",
                            "Write and reuse functions",
                            "Solve small problems with structured thinking"
                        },
                        "MOD-PYBASIC-03" => new[]
                        {
                            "Apply Python fundamentals in a mini-project",
                            "Plan simple game logic and implement it",
                            "Test and refine features"
                        },
                        "MOD-MATHFUN-01" => new[]
                        {
                            "Build number sense through games",
                            "Practice basic arithmetic strategies",
                            "Develop confidence with math challenges"
                        },
                        "MOD-MATHFUN-02" => new[]
                        {
                            "Recognize patterns and sequences",
                            "Solve puzzles using logical reasoning",
                            "Explain solutions clearly"
                        },
                        "MOD-DIGART-01" => new[]
                        {
                            "Use basic digital drawing tools",
                            "Apply color and composition principles",
                            "Create simple artwork with layers"
                        },
                        "MOD-DIGART-02" => new[]
                        {
                            "Design characters or scenes digitally",
                            "Improve line, shading, and texture",
                            "Export artwork for sharing"
                        },
                        "MOD-BIOTECH-01" => new[]
                        {
                            "Understand basic biology and lab safety",
                            "Observe and document simple experiments",
                            "Learn scientific method basics"
                        },
                        "MOD-3DDESIGN-01" => new[]
                        {
                            "Model simple 3D shapes and objects",
                            "Understand dimensions and constraints",
                            "Prepare models for printing or presentation"
                        },
                        "MOD-AIBASIC-01" => new[]
                        {
                            "Understand what AI is and common applications",
                            "Learn basic ML concepts (data, training, prediction)",
                            "Discuss AI ethics at a beginner level"
                        },
                        _ => new[]
                        {
                            "Understand key concepts of this module",
                            "Practice skills through hands-on activities",
                            "Apply learning in a small project or assessment"
                        }
                    };
                }

                await _unitOfWork.Modules.UpdateRange(modulesToBackfill);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation(
                    "Backfilled LearningOutcomes for {Count} existing module(s).",
                    modulesToBackfill.Count);
            }
        }

    }
}

