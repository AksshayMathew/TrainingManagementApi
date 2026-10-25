namespace TrainingManagementApi.Models;

/// <summary>A training course that employees can be assigned to.</summary>
public class Course
{
    public int CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    /// <summary>Length of the course in hours; always greater than 0.</summary>
    public int Duration { get; set; }

    public string TrainerName { get; set; } = string.Empty;
}
