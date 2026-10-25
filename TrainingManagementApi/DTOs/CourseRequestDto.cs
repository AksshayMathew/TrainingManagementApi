using System.ComponentModel.DataAnnotations;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.DTOs;

/// <summary>Data a client sends to create (POST) or fully replace (PUT) a course.</summary>
public class CourseRequestDto
{
    /// <summary>Title of the course.</summary>
    /// <example>ASP.NET Core Web API Fundamentals</example>
    [Required(ErrorMessage = "Course name is required.")]
    [StringLength(FieldLimits.CourseNameMaxLength,
        ErrorMessage = "Course name cannot be longer than {1} characters.")]
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Length of the course in hours. Must be greater than 0. (Nullable here so a missing value is reported instead of becoming 0.)</summary>
    /// <example>16</example>
    [Required(ErrorMessage = "Duration is required.")]
    [Range(1, FieldLimits.MaxCourseDuration,
        ErrorMessage = "Duration must be greater than 0 and cannot be more than {2}.")]
    public int? Duration { get; set; }

    /// <summary>Name of the person who delivers the course.</summary>
    /// <example>Anita Desai</example>
    [Required(ErrorMessage = "Trainer name is required.")]
    [StringLength(FieldLimits.TrainerNameMaxLength,
        ErrorMessage = "Trainer name cannot be longer than {1} characters.")]
    public string TrainerName { get; set; } = string.Empty;
}
