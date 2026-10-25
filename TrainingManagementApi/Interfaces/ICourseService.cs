using TrainingManagementApi.DTOs;

namespace TrainingManagementApi.Interfaces;

public interface ICourseService
{
    Task<IReadOnlyList<CourseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No course has this id.</exception>
    Task<CourseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<CourseDto> CreateAsync(CourseRequestDto request, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No course has this id.</exception>
    Task<CourseDto> UpdateAsync(int id, CourseRequestDto request, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No course has this id.</exception>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
