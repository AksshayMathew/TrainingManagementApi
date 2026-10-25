namespace TrainingManagementApi.DTOs;

/// <summary>A course as returned by the API.</summary>
public class CourseDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Length of the course in hours.</summary>
    public int Duration { get; set; }

    public string TrainerName { get; set; } = string.Empty;
}
