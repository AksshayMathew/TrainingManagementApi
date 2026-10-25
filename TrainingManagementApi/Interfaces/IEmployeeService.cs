using TrainingManagementApi.DTOs;

namespace TrainingManagementApi.Interfaces;

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No employee has this id.</exception>
    Task<EmployeeDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.BusinessRuleException">The e-mail address is already used.</exception>
    Task<EmployeeDto> CreateAsync(EmployeeRequestDto request, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No employee has this id.</exception>
    /// <exception cref="Exceptions.BusinessRuleException">The e-mail address belongs to another employee.</exception>
    Task<EmployeeDto> UpdateAsync(int id, EmployeeRequestDto request, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.NotFoundException">No employee has this id.</exception>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
