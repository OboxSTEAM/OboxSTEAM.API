using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CourseDTO;

namespace OboxSteam.Application.Interfaces;

public interface ICourseService
{
    Task<Pagination<CourseResponseDto>> GetAllCoursesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code,
        string? moduleName,
        string? mentorName);

    Task<CourseResponseDto?> GetCourseByIdAsync(Guid courseId);

    Task<CourseResponseDto?> GetCourseByNameAsync(string? courseName);

    Task<CourseResponseDto> CreateCourseAsync(CreateCourseRequestDto request);

    Task<CourseResponseDto?> UpdateCourseAsync(Guid courseId, UpdateCourseRequestDto request);

    Task<bool> DeleteCourseAsync(Guid courseId);
}
