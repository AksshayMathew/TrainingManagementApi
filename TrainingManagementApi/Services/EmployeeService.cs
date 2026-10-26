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

    public async Task<PagedResult<EmployeeDto>> GetAllAsync(EmployeeQuery query, CancellationToken cancellationToken = default)
    {
        var employees = ApplyFilters(_db.Employees.AsNoTracking(), query);
        var ordered = ApplySorting(employees, query);

        if (!query.IsPaged)
        {
            var all = await ordered.ToListAsync(cancellationToken);
            return new PagedResult<EmployeeDto> { Items = all.Select(MapToDto).ToList(), TotalCount = all.Count };
        }

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? EmployeeQuery.DefaultPageSize;

        var totalCount = await employees.CountAsync(cancellationToken);
        var pageItems = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeDto>
        {
            Items = pageItems.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
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

    private static IQueryable<Employee> ApplyFilters(IQueryable<Employee> employees, EmployeeQuery query)
    {
        // Lower-casing both sides makes the comparison case-insensitive on every database.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            employees = employees.Where(e =>
                e.EmployeeName.ToLower().Contains(term) || e.Department.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Department))
        {
            var department = query.Department.Trim().ToLowerInvariant();
            employees = employees.Where(e => e.Department.ToLower() == department);
        }

        return employees;
    }

    private static IOrderedQueryable<Employee> ApplySorting(IQueryable<Employee> employees, EmployeeQuery query)
    {
        var descending = query.SortOrder == SortOrder.Desc;

        var ordered = query.SortBy switch
        {
            EmployeeSortField.Name => descending
                ? employees.OrderByDescending(e => e.EmployeeName) : employees.OrderBy(e => e.EmployeeName),
            EmployeeSortField.Department => descending
                ? employees.OrderByDescending(e => e.Department) : employees.OrderBy(e => e.Department),
            EmployeeSortField.JoiningDate => descending
                ? employees.OrderByDescending(e => e.JoiningDate) : employees.OrderBy(e => e.JoiningDate),
            EmployeeSortField.Experience => descending
                ? employees.OrderByDescending(e => e.Experience) : employees.OrderBy(e => e.Experience),
            _ => descending
                ? employees.OrderByDescending(e => e.EmployeeId) : employees.OrderBy(e => e.EmployeeId)
        };

        // Tie-breaker: without it, rows with equal values could swap places between pages.
        return query.SortBy == EmployeeSortField.Id ? ordered : ordered.ThenBy(e => e.EmployeeId);
    }

    /// <summary>E-mail addresses are compared without regard to case, so we always store them trimmed and lower-case.</summary>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static BusinessRuleException EmailAlreadyInUse() =>
        new(nameof(EmployeeRequestDto.Email), "An employee with this email already exists. Use a different e-mail address.");

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
