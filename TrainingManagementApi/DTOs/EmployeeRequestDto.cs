using System.ComponentModel.DataAnnotations;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.DTOs;

/// <summary>Data a client sends to create (POST) or fully replace (PUT) an employee.</summary>
public class EmployeeRequestDto
{
    /// <summary>Full name of the employee.</summary>
    /// <example>Priya Sharma</example>
    [Required(ErrorMessage = "Employee name is required.")]
    [StringLength(FieldLimits.EmployeeNameMaxLength,
        ErrorMessage = "Employee name cannot be longer than {1} characters.")]
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>Work e-mail address. Must be unique (not case-sensitive).</summary>
    /// <example>priya.sharma@abctechnologies.com</example>
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid e-mail address.")]
    [StringLength(FieldLimits.EmailMaxLength,
        ErrorMessage = "Email cannot be longer than {1} characters.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Department the employee belongs to.</summary>
    /// <example>Engineering</example>
    [Required(ErrorMessage = "Department is required.")]
    [StringLength(FieldLimits.DepartmentMaxLength,
        ErrorMessage = "Department cannot be longer than {1} characters.")]
    public string Department { get; set; } = string.Empty;

    /// <summary>Date the employee joined. (Nullable here so a missing value is reported instead of silently becoming 0001-01-01.)</summary>
    /// <example>2026-01-15T00:00:00</example>
    [Required(ErrorMessage = "Joining date is required.")]
    public DateTime? JoiningDate { get; set; }

    /// <summary>Years of experience. Cannot be negative.</summary>
    /// <example>3</example>
    [Required(ErrorMessage = "Experience is required.")]
    [Range(0, FieldLimits.MaxExperienceYears,
        ErrorMessage = "Experience cannot be negative and cannot be more than {2} years.")]
    public int? Experience { get; set; }
}
