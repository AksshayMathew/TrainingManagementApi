namespace TrainingManagementApi.DTOs;

/// <summary>A list of items plus how many items matched in total (before paging).</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Number of items that matched the search/filter, across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>The page that was returned, or null when paging was not requested.</summary>
    public int? Page { get; init; }

    public int? PageSize { get; init; }
}
