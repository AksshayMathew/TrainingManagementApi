namespace TrainingManagementApi.Models;

/// <summary>The allowed values of <see cref="TrainingAssignment.Status"/>, plus lenient parsing of client input.</summary>
public static class TrainingStatuses
{
    public const string Assigned = "Assigned";
    public const string InProgress = "In Progress";
    public const string Completed = "Completed";

    public static readonly IReadOnlyList<string> All = new[] { Assigned, InProgress, Completed };

    /// <summary>The message shown to clients when a status is not one of <see cref="All"/>.</summary>
    public static string InvalidStatusMessage => $"Status must be one of: {string.Join(", ", All)}.";

    /// <summary>
    /// Matches a status ignoring case, spaces, hyphens and underscores, so "in progress", "In-Progress" and
    /// "InProgress" all become the canonical "In Progress". Anything else (e.g. "Done", "Completed2") is rejected.
    /// </summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        var key = Squash(value);

        foreach (var status in All)
        {
            if (key.Equals(Squash(status), StringComparison.OrdinalIgnoreCase))
            {
                normalized = status;
                return true;
            }
        }

        normalized = string.Empty;
        return false;
    }

    private static string Squash(string? value) =>
        new((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray());
}
