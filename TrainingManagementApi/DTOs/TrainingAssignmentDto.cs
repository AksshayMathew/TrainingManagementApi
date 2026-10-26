namespace TrainingManagementApi.DTOs;

/// <summary>A training assignment as returned by the API, including the employee and course names for convenience.</summary>
public class TrainingAssignmentDto
{
    /// <summary>Id given by the system. Use it to change the status or delete the assignment.</summary>
    /// <example>1</example>
    public int AssignmentId { get; set; }

    /// <example>1</example>
    public int EmployeeId { get; set; }

    /// <example>Priya Sharma</example>
    public string EmployeeName { get; set; } = string.Empty;

    /// <example>1</example>
    public int CourseId { get; set; }

    /// <example>ASP.NET Core Web API Fundamentals</example>
    public string CourseName { get; set; } = string.Empty;

    /// <example>Anita Desai</example>
    public string TrainerName { get; set; } = string.Empty;

    /// <example>2026-02-01T00:00:00</example>
    public DateTime StartDate { get; set; }

    /// <example>2026-02-05T00:00:00</example>
    public DateTime EndDate { get; set; }

    /// <summary>Assigned, In Progress or Completed.</summary>
    /// <example>Assigned</example>
    public string Status { get; set; } = string.Empty;
}
