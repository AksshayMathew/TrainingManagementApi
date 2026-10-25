using TrainingManagementApi.DTOs;

namespace TrainingManagementApi.Interfaces;

public interface ITrainingAssignmentService
{
    /// <exception cref="Exceptions.NotFoundException">The employee or the course does not exist.</exception>
    /// <exception cref="Exceptions.BusinessRuleException">The employee already has this course.</exception>
    Task<TrainingAssignmentDto> AssignAsync(TrainingAssignmentRequestDto request, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No assignment has this id.</exception>
    Task<TrainingAssignmentDto> GetByIdAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No employee has this id.</exception>
    Task<IReadOnlyList<TrainingAssignmentDto>> GetForEmployeeAsync(int employeeId, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No assignment has this id.</exception>
    Task<TrainingAssignmentDto> UpdateStatusAsync(int assignmentId, UpdateTrainingStatusDto request, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No assignment has this id.</exception>
    Task DeleteAsync(int assignmentId, CancellationToken cancellationToken = default);
}
