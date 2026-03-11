namespace AgentBoard.Contracts;

/// <summary>
/// Generic wrapper for paginated API responses.
/// </summary>
/// <typeparam name="T">The item type being paginated.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>The items on the current page.</summary>
    public List<T> Items { get; init; } = [];

    /// <summary>Total number of items matching the current filter, across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>The current 1-based page number.</summary>
    public int Page { get; init; }

    /// <summary>The number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Creates a <see cref="PagedResult{T}"/> from a pre-fetched page of items and a total count.
    /// </summary>
    public static PagedResult<T> Create(List<T> items, int totalCount, int page, int pageSize) => new()
    {
        Items = items,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize,
        TotalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0
    };
}
