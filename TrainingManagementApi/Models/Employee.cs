namespace TrainingManagementApi.Models;

/// <summary>A person working at ABC Technologies who can be assigned training courses.</summary>
public class Employee
{
    public int EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>Stored trimmed and lower-case so uniqueness is case-insensitive on every database.</summary>
    public string Email { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public DateTime JoiningDate { get; set; }

    /// <summary>Years of experience; 0 or greater.</summary>
    public int Experience { get; set; }

    /// <summary>One employee has many training assignments.</summary>
    public ICollection<TrainingAssignment> TrainingAssignments { get; set; } = new List<TrainingAssignment>();
}
