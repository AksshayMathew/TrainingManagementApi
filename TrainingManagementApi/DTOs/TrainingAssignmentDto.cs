namespace TrainingManagementApi.DTOs;

/// <summary>A training assignment as returned by the API, including the employee and course names for convenience.</summary>
public class TrainingAssignmentDto
{
    public int AssignmentId { get; set; }

    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string TrainerName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Assigned, In Progress or Completed.</summary>
    public string Status { get; set; } = string.Empty;
}
