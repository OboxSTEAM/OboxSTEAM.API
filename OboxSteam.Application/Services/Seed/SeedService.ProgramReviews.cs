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
    private async Task SeedProgramReviewsAsync()
    {
        _loggerService.LogInformation("Starting seed program reviews");
        var existingProgramReviews = await _unitOfWork.ProgramReviews.GetAllAsync();
        if (!existingProgramReviews.Any())
        {
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");
            var student4 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-004");
            var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
            var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
            var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");
            var programPyBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-PYBASIC");
            var programGameDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-GAMEDEV");
            var reviewTime = DateTime.UtcNow;

            var programReviews = new List<ProgramReview>();

            if (student1 != null && programRobotics != null)
            {
                programReviews.Add(new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programRobotics.Id,
                    StudentId = student1.Id,
                    StarRating = 5,
                    Comment = "Chương trình thực sự thú vị! Tôi đã học được rất nhiều về robotics từ căn bản đến nâng cao. Các hoạt động thực hành rất bổ ích.",
                    CreatedAt = reviewTime.AddDays(-10),
                    CreatedBy = student1.Id,
                    IsDeleted = false
                });
            }

            if (student2 != null && programRobotics != null)
            {
                programReviews.Add(new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programRobotics.Id,
                    StudentId = student2.Id,
                    StarRating = 4,
                    Comment = "Nội dung phong phú, mentor nhiệt tình. Chỉ tiếc thời lượng hơi ngắn so với khối lượng kiến thức.",
                    CreatedAt = reviewTime.AddDays(-8),
                    CreatedBy = student2.Id,
                    IsDeleted = false
                });
            }

            if (student1 != null && programWebDev != null)
            {
                programReviews.Add(new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programWebDev.Id,
                    StudentId = student1.Id,
                    StarRating = 5,
                    Comment = "Bootcamp web dev cực hay! Sau khoá học tôi đã tự xây dựng được trang web cá nhân. Rất đáng tiền.",
                    CreatedAt = reviewTime.AddDays(-6),
                    CreatedBy = student1.Id,
                    IsDeleted = false
                });
            }

            if (student3 != null && programWebDev != null)
            {
                programReviews.Add(new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programWebDev.Id,
                    StudentId = student3.Id,
                    StarRating = 4,
                    Comment = "Giảng viên giải thích rõ ràng, bài tập thực hành đa dạng. Tôi đã cải thiện kỹ năng CSS rất nhiều.",
                    CreatedAt = reviewTime.AddDays(-5),
                    CreatedBy = student3.Id,
                    IsDeleted = false
                });
            }

            if (student2 != null && programIot != null)
            {
                programReviews.Add(new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programIot.Id,
                    StudentId = student2.Id,
                    StarRating = 4,
                    Comment = "IoT Fundamentals rất bổ ích cho ai muốn tìm hiểu về thiết bị thông minh. Phần cloud connectivity là điểm nhấn.",
                    CreatedAt = reviewTime.AddDays(-12),
                    CreatedBy = student2.Id,
                    IsDeleted = false
                });
            }

            if (student4 != null && programIot != null)
            {
                programReviews.Add(new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programIot.Id,
                    StudentId = student4.Id,
                    StarRating = 3,
                    Comment = "Nội dung ổn nhưng cần thêm tài liệu tham khảo bằng tiếng Việt. Phần thực hành cần nhiều kit hơn.",
                    CreatedAt = reviewTime.AddDays(-3),
                    CreatedBy = student4.Id,
                    IsDeleted = false
                });
            }

            if (student1 != null && programPyBasic != null)
            {
                programReviews.Add(new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programPyBasic.Id,
                    StudentId = student1.Id,
                    StarRating = 5,
                    Comment = "Khoá Python cho người mới bắt đầu này cực kỳ dễ hiểu! Tôi chưa có kinh nghiệm lập trình nhưng sau 6 tuần đã viết được game nhỏ.",
                    CreatedAt = reviewTime.AddDays(-15),
                    CreatedBy = student1.Id,
                    IsDeleted = false
                });
            }

            if (student3 != null && programGameDev != null)
            {
                programReviews.Add(new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programGameDev.Id,
                    StudentId = student3.Id,
                    StarRating = 5,
                    Comment = "Game Design & Development là khoá học yêu thích nhất của tôi! Tôi đã publish được game 2D đầu tiên sau khi hoàn thành.",
                    CreatedAt = reviewTime.AddDays(-7),
                    CreatedBy = student3.Id,
                    IsDeleted = false
                });
            }

            if (student4 != null && programGameDev != null)
            {
                programReviews.Add(new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = programGameDev.Id,
                    StudentId = student4.Id,
                    StarRating = 4,
                    Comment = "Nội dung phong phú, hướng dẫn chi tiết từng bước. Phần sprite animation rất thú vị và sáng tạo.",
                    CreatedAt = reviewTime.AddDays(-2),
                    CreatedBy = student4.Id,
                    IsDeleted = false
                });
            }

            if (programReviews.Count > 0)
            {
                await _unitOfWork.ProgramReviews.AddRangeAsync(programReviews);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed program reviews — {Count} review(s) created.", programReviews.Count);
            }
            else
            {
                _loggerService.LogWarning("No program reviews seeded.");
            }
        }
        else
        {
            _loggerService.LogInformation("Program reviews already exist, skipping seeding");
        }
    }
}

