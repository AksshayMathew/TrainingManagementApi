namespace TrainingManagementApi.DTOs;

/// <summary>An employee as returned by the API.</summary>
public class EmployeeDto
{
    /// <summary>Id given by the system. Use it in other requests (get, update, delete, assign training).</summary>
    /// <example>1</example>
    public int EmployeeId { get; set; }

    /// <example>Priya Sharma</example>
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>Always stored in lower case.</summary>
    /// <example>priya.sharma@abctechnologies.com</example>
    public string Email { get; set; } = string.Empty;

    /// <example>Engineering</example>
    public string Department { get; set; } = string.Empty;

    /// <example>2026-01-15T00:00:00</example>
    public DateTime JoiningDate { get; set; }

    /// <summary>Years of experience.</summary>
    /// <example>3</example>
    public int Experience { get; set; }
}
