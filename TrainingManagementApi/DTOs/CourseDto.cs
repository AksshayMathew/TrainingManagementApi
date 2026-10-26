namespace TrainingManagementApi.DTOs;

/// <summary>A course as returned by the API.</summary>
public class CourseDto
{
    /// <summary>Id given by the system. Use it when assigning the course to an employee.</summary>
    /// <example>1</example>
    public int CourseId { get; set; }

    /// <example>ASP.NET Core Web API Fundamentals</example>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Length of the course in hours.</summary>
    /// <example>16</example>
    public int Duration { get; set; }

    /// <example>Anita Desai</example>
    public string TrainerName { get; set; } = string.Empty;
}
