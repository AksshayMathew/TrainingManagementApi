using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrainingManagementApi.Data;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Exceptions;
using TrainingManagementApi.Interfaces;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.Services;

public class TrainingAssignmentService : ITrainingAssignmentService
{
    private const string AssignmentEntityName = "Training assignment";

    /// <summary>The one place that decides what an assignment looks like in API responses (translated to SQL by EF Core).</summary>
    private static readonly Expression<Func<TrainingAssignment, TrainingAssignmentDto>> ToDto = a => new TrainingAssignmentDto
    {
        AssignmentId = a.AssignmentId,
        EmployeeId = a.EmployeeId,
        EmployeeName = a.Employee.EmployeeName,
        CourseId = a.CourseId,
        CourseName = a.Course.CourseName,
        TrainerName = a.Course.TrainerName,
        StartDate = a.StartDate,
        EndDate = a.EndDate,
        Status = a.Status
    };

    private readonly AppDbContext _db;
    private readonly ILogger<TrainingAssignmentService> _logger;

    public TrainingAssignmentService(AppDbContext db, ILogger<TrainingAssignmentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TrainingAssignmentDto> AssignAsync(
        TrainingAssignmentRequestDto request, CancellationToken cancellationToken = default)
    {
        // Model validation guarantees these have values (they are nullable only so "missing" can be reported).
        var employeeId = request.EmployeeId!.Value;
        var courseId = request.CourseId!.Value;

        if (!await _db.Employees.AnyAsync(e => e.EmployeeId == employeeId, cancellationToken))
        {
            throw new NotFoundException(nameof(Employee), employeeId);
        }

        if (!await _db.Courses.AnyAsync(c => c.CourseId == courseId, cancellationToken))
        {
            throw new NotFoundException(nameof(Course), courseId);
        }

        if (await IsAlreadyAssignedAsync(employeeId, courseId, cancellationToken))
        {
            throw CourseAlreadyAssigned();
        }

        var assignment = new TrainingAssignment
        {
            EmployeeId = employeeId,
            CourseId = courseId,
            StartDate = request.StartDate!.Value,
            EndDate = request.EndDate!.Value,
            Status = request.Status is null ? TrainingStatuses.Assigned : NormalizeStatus(request.Status)
        };
        _db.TrainingAssignments.Add(assignment);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Something changed between our checks and the save (another request got there first).
            // Work out what, so the client gets a precise message instead of a generic 500.
            _db.Entry(assignment).State = EntityState.Detached;

            if (await IsAlreadyAssignedAsync(employeeId, courseId, cancellationToken))
            {
                throw CourseAlreadyAssigned();
            }

            if (!await _db.Employees.AnyAsync(e => e.EmployeeId == employeeId, cancellationToken))
            {
                throw new NotFoundException(nameof(Employee), employeeId);
            }

            if (!await _db.Courses.AnyAsync(c => c.CourseId == courseId, cancellationToken))
            {
                throw new NotFoundException(nameof(Course), courseId);
            }

            throw;
        }

        _logger.LogInformation(
            "Course assigned. AssignmentId={AssignmentId} EmployeeId={EmployeeId} CourseId={CourseId}",
            assignment.AssignmentId, employeeId, courseId);

        return await GetByIdAsync(assignment.AssignmentId, cancellationToken);
    }

    public async Task<TrainingAssignmentDto> GetByIdAsync(int assignmentId, CancellationToken cancellationToken = default)
    {
        return await _db.TrainingAssignments
            .AsNoTracking()
            .Where(a => a.AssignmentId == assignmentId)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AssignmentEntityName, assignmentId);
    }

    public async Task<IReadOnlyList<TrainingAssignmentDto>> GetForEmployeeAsync(
        int employeeId, CancellationToken cancellationToken = default)
    {
        // An unknown employee is a 404, not an empty list - an empty list means "known employee, no trainings".
        if (!await _db.Employees.AnyAsync(e => e.EmployeeId == employeeId, cancellationToken))
        {
            throw new NotFoundException(nameof(Employee), employeeId);
        }

        return await _db.TrainingAssignments
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId)
            .OrderBy(a => a.StartDate)
            .ThenBy(a => a.AssignmentId)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<TrainingAssignmentDto> UpdateStatusAsync(
        int assignmentId, UpdateTrainingStatusDto request, CancellationToken cancellationToken = default)
    {
        var assignment = await _db.TrainingAssignments
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, cancellationToken)
            ?? throw new NotFoundException(AssignmentEntityName, assignmentId);

        assignment.Status = NormalizeStatus(request.Status);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The row was deleted by someone else between our read and our save.
            throw new NotFoundException(AssignmentEntityName, assignmentId);
        }

        _logger.LogInformation("Training status updated. AssignmentId={AssignmentId} Status={Status}",
            assignmentId, assignment.Status);

        return await GetByIdAsync(assignmentId, cancellationToken);
    }

    public async Task DeleteAsync(int assignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await _db.TrainingAssignments
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, cancellationToken)
            ?? throw new NotFoundException(AssignmentEntityName, assignmentId);

        _db.TrainingAssignments.Remove(assignment);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotFoundException(AssignmentEntityName, assignmentId);
        }

        _logger.LogInformation("Training assignment deleted. AssignmentId={AssignmentId}", assignmentId);
    }

    private Task<bool> IsAlreadyAssignedAsync(int employeeId, int courseId, CancellationToken cancellationToken) =>
        _db.TrainingAssignments.AnyAsync(a => a.EmployeeId == employeeId && a.CourseId == courseId, cancellationToken);

    private static BusinessRuleException CourseAlreadyAssigned() =>
        new(nameof(TrainingAssignmentRequestDto.CourseId), "This employee is already assigned to this course.");

    /// <summary>Turns "in progress" / "IN-PROGRESS" / "InProgress" into the canonical "In Progress", or rejects it.</summary>
    private static string NormalizeStatus(string? status) =>
        TrainingStatuses.TryNormalize(status, out var normalized)
            ? normalized
            : throw new BusinessRuleException(nameof(UpdateTrainingStatusDto.Status), TrainingStatuses.InvalidStatusMessage);
}
