namespace TrainingManagementApi.Tests.Infrastructure;

/// <summary>Builders for valid request bodies. Every call produces unique values so tests never collide.</summary>
public static class TestData
{
    public static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    public static object NewEmployee(
        string? email = null,
        string name = "Test Person",
        string department = "Engineering",
        int experience = 3) => new
    {
        employeeName = name,
        email = email ?? UniqueEmail(),
        department,
        joiningDate = "2026-01-15T00:00:00",
        experience
    };

    public static object NewCourse(
        string name = "Intro to Testing",
        int duration = 8,
        string trainer = "Anita Desai") => new
    {
        courseName = name,
        duration,
        trainerName = trainer
    };
}
