namespace TrainingManagementApi.Models;

/// <summary>The allowed values of <see cref="TrainingAssignment.Status"/>, plus lenient parsing of client input.</summary>
public static class TrainingStatuses
{
    public const string Assigned = "Assigned";
    public const string InProgress = "In Progress";
    public const string Completed = "Completed";

    public static readonly IReadOnlyList<string> All = new[] { Assigned, InProgress, Completed };

    /// <summary>
    /// Matches a status ignoring case, spaces and punctuation, so "in progress", "In-Progress" and
    /// "InProgress" all become the canonical "In Progress".
    /// </summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        var key = LettersOnly(value);

        foreach (var status in All)
        {
            if (key.Equals(LettersOnly(status), StringComparison.OrdinalIgnoreCase))
            {
                normalized = status;
                return true;
            }
        }

        normalized = string.Empty;
        return false;
    }

    private static string LettersOnly(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetter).ToArray());
}
