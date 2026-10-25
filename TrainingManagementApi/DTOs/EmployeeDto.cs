namespace TrainingManagementApi.DTOs;

/// <summary>An employee as returned by the API.</summary>
public class EmployeeDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime JoiningDate { get; set; }
    public int Experience { get; set; }
}
