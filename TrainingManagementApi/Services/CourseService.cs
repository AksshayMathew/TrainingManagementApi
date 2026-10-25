using Microsoft.EntityFrameworkCore;
using TrainingManagementApi.Data;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Exceptions;
using TrainingManagementApi.Interfaces;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.Services;

public class CourseService : ICourseService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CourseService> _logger;

    public CourseService(AppDbContext db, ILogger<CourseService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CourseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var courses = await _db.Courses
            .AsNoTracking()
            .OrderBy(c => c.CourseId)
            .ToListAsync(cancellationToken);

        return courses.Select(MapToDto).ToList();
    }

    public async Task<CourseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var course = await _db.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CourseId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), id);

        return MapToDto(course);
    }

    public async Task<CourseDto> CreateAsync(CourseRequestDto request, CancellationToken cancellationToken = default)
    {
        var course = new Course();
        ApplyRequest(course, request);
        _db.Courses.Add(course);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Course created. CourseId={CourseId}", course.CourseId);
        return MapToDto(course);
    }

    public async Task<CourseDto> UpdateAsync(int id, CourseRequestDto request, CancellationToken cancellationToken = default)
    {
        var course = await _db.Courses
            .FirstOrDefaultAsync(c => c.CourseId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), id);

        ApplyRequest(course, request);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The row was deleted by someone else between our read and our save.
            throw new NotFoundException(nameof(Course), id);
        }

        _logger.LogInformation("Course updated. CourseId={CourseId}", id);
        return MapToDto(course);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var course = await _db.Courses
            .FirstOrDefaultAsync(c => c.CourseId == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Course), id);

        _db.Courses.Remove(course);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotFoundException(nameof(Course), id);
        }

        _logger.LogInformation("Course deleted. CourseId={CourseId}", id);
    }

    private static void ApplyRequest(Course course, CourseRequestDto request)
    {
        // Duration is nullable in the DTO only so a missing value is reported as a validation error;
        // by the time we get here model validation has guaranteed it has a value.
        course.CourseName = request.CourseName.Trim();
        course.Duration = request.Duration!.Value;
        course.TrainerName = request.TrainerName.Trim();
    }

    private static CourseDto MapToDto(Course course) => new()
    {
        CourseId = course.CourseId,
        CourseName = course.CourseName,
        Duration = course.Duration,
        TrainerName = course.TrainerName
    };
}
