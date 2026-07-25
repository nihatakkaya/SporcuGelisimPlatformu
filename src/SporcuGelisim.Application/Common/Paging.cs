namespace SporcuGelisim.Application.Common;

public sealed record PagedRequest(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, bool Descending = false)
{
    public int Skip => (Math.Max(Page, 1) - 1) * Math.Clamp(PageSize, 1, 100);
    public int Take => Math.Clamp(PageSize, 1, 100);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
