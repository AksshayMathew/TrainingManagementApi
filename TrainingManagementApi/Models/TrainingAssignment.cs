namespace TrainingManagementApi.Models;

/// <summary>Links one <see cref="Employee"/> to one <see cref="Course"/> for a period of time.</summary>
public class TrainingAssignment
{
    public int AssignmentId { get; set; }

    public int EmployeeId { get; set; }

    public int CourseId { get; set; }

    public DateTime StartDate { get; set; }

    /// <summary>On or after <see cref="StartDate"/>.</summary>
    public DateTime EndDate { get; set; }

    /// <summary>One of <see cref="TrainingStatuses.All"/>.</summary>
    public string Status { get; set; } = TrainingStatuses.Assigned;

    public Employee Employee { get; set; } = null!;

    public Course Course { get; set; } = null!;
}
