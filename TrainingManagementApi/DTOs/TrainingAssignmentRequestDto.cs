using System.ComponentModel.DataAnnotations;

namespace TrainingManagementApi.DTOs;

/// <summary>Data a client sends to assign a course to an employee.</summary>
public class TrainingAssignmentRequestDto : IValidatableObject
{
    /// <summary>Id of an existing employee.</summary>
    /// <example>1</example>
    [Required(ErrorMessage = "Employee id is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Employee id must be a positive number.")]
    public int? EmployeeId { get; set; }

    /// <summary>Id of an existing course.</summary>
    /// <example>1</example>
    [Required(ErrorMessage = "Course id is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Course id must be a positive number.")]
    public int? CourseId { get; set; }

    /// <summary>First day of the training.</summary>
    /// <example>2026-02-01T00:00:00</example>
    [Required(ErrorMessage = "Start date is required.")]
    public DateTime? StartDate { get; set; }

    /// <summary>Last day of the training. Must be on or after the start date.</summary>
    /// <example>2026-02-05T00:00:00</example>
    [Required(ErrorMessage = "End date is required.")]
    public DateTime? EndDate { get; set; }

    /// <summary>Optional. One of Assigned, In Progress, Completed. Defaults to Assigned.</summary>
    /// <example>Assigned</example>
    [TrainingStatus]
    public string? Status { get; set; }

    // Runs only after every attribute above passed, so both dates are present here.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
        {
            yield return new ValidationResult(
                "End date cannot be before start date.", new[] { nameof(EndDate) });
        }
    }
}
