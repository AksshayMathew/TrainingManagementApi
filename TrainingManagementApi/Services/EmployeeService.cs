using Microsoft.EntityFrameworkCore;
using TrainingManagementApi.Data;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Exceptions;
using TrainingManagementApi.Interfaces;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.Services;

public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(AppDbContext db, ILogger<EmployeeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var employees = await _db.Employees
            .AsNoTracking()
            .OrderBy(e => e.EmployeeId)
            .ToListAsync(cancellationToken);

        return employees.Select(MapToDto).ToList();
    }

    public async Task<EmployeeDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), id);

        return MapToDto(employee);
    }

    public async Task<EmployeeDto> CreateAsync(EmployeeRequestDto request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        if (await _db.Employees.AnyAsync(e => e.Email == email, cancellationToken))
        {
            throw EmailAlreadyInUse();
        }

        var employee = new Employee();
        ApplyRequest(employee, request, email);
        _db.Employees.Add(employee);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two requests with the same e-mail can both pass the check above at the same moment;
            // the unique index lets only one win. Report the loser the same friendly way.
            _db.Entry(employee).State = EntityState.Detached;
            if (await _db.Employees.AnyAsync(e => e.Email == email, cancellationToken))
            {
                throw EmailAlreadyInUse();
            }

            throw;
        }

        _logger.LogInformation("Employee created. EmployeeId={EmployeeId}", employee.EmployeeId);
        return MapToDto(employee);
    }

    public async Task<EmployeeDto> UpdateAsync(int id, EmployeeRequestDto request, CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.EmployeeId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), id);

        var email = NormalizeEmail(request.Email);

        if (await _db.Employees.AnyAsync(e => e.Email == email && e.EmployeeId != id, cancellationToken))
        {
            throw EmailAlreadyInUse();
        }

        ApplyRequest(employee, request, email);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The row was deleted by someone else between our read and our save.
            throw new NotFoundException(nameof(Employee), id);
        }
        catch (DbUpdateException)
        {
            if (await _db.Employees.AnyAsync(e => e.Email == email && e.EmployeeId != id, cancellationToken))
            {
                throw EmailAlreadyInUse();
            }

            throw;
        }

        _logger.LogInformation("Employee updated. EmployeeId={EmployeeId}", id);
        return MapToDto(employee);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.EmployeeId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), id);

        // Training history is never deleted by accident: the assignments must be removed first.
        if (await HasTrainingAssignmentsAsync(id, cancellationToken))
        {
            throw EmployeeHasAssignments();
        }

        _db.Employees.Remove(employee);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotFoundException(nameof(Employee), id);
        }
        catch (DbUpdateException)
        {
            // An assignment was added after our check; the foreign key refused the delete.
            if (await HasTrainingAssignmentsAsync(id, cancellationToken))
            {
                throw EmployeeHasAssignments();
            }

            throw;
        }

        _logger.LogInformation("Employee deleted. EmployeeId={EmployeeId}", id);
    }

    private Task<bool> HasTrainingAssignmentsAsync(int employeeId, CancellationToken cancellationToken) =>
        _db.TrainingAssignments.AnyAsync(a => a.EmployeeId == employeeId, cancellationToken);

    private static BusinessRuleException EmployeeHasAssignments() =>
        new("EmployeeId", "This employee still has training assignments. Delete those assignments first.");

    /// <summary>E-mail addresses are compared without regard to case, so we always store them trimmed and lower-case.</summary>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static BusinessRuleException EmailAlreadyInUse() =>
        new(nameof(EmployeeRequestDto.Email), "An employee with this email already exists.");

    private static void ApplyRequest(Employee employee, EmployeeRequestDto request, string normalizedEmail)
    {
        // JoiningDate/Experience are nullable in the DTO only so a missing value is reported as a validation
        // error; by the time we get here model validation has guaranteed they have values.
        employee.EmployeeName = request.EmployeeName.Trim();
        employee.Email = normalizedEmail;
        employee.Department = request.Department.Trim();
        employee.JoiningDate = request.JoiningDate!.Value;
        employee.Experience = request.Experience!.Value;
    }

    private static EmployeeDto MapToDto(Employee employee) => new()
    {
        EmployeeId = employee.EmployeeId,
        EmployeeName = employee.EmployeeName,
        Email = employee.Email,
        Department = employee.Department,
        JoiningDate = employee.JoiningDate,
        Experience = employee.Experience
    };
}
