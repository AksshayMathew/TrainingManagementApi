namespace TrainingManagementApi.Models;

/// <summary>
/// Single source of truth for column sizes and numeric limits. The EF Core mapping (database) and the
/// DTO validation attributes (API) both read these constants, so the two can never disagree.
/// </summary>
public static class FieldLimits
{
    public const int EmployeeNameMaxLength = 100;
    public const int EmailMaxLength = 254;          // practical maximum length of an e-mail address
    public const int DepartmentMaxLength = 100;
    public const int MaxExperienceYears = 60;

    public const int CourseNameMaxLength = 150;
    public const int TrainerNameMaxLength = 100;
    public const int MaxCourseDuration = 10_000;    // upper sanity bound; the rule itself is "> 0"

    public const int StatusMaxLength = 20;
}
