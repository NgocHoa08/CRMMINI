namespace CRMMini.Application.Common;

using Microsoft.EntityFrameworkCore;

public record PaginationQuery
{
    private const int MaxPageSize = 100;
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? 20 : (value > MaxPageSize ? MaxPageSize : value);
    }

    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public string? SortDir { get; init; }

    public bool IsDescending => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public static class QueryablePaginationExtensions
{
    public static async Task<PagedResult<TDto>> ToPagedResultAsync<TEntity, TDto>(
        this IQueryable<TEntity> query,
        PaginationQuery pagination,
        Func<IEnumerable<TEntity>, IEnumerable<TDto>> mapItems,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var entities = totalCount == 0
            ? new List<TEntity>()
            : await query
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

        var items = mapItems(entities).ToList();
        return new PagedResult<TDto>(items, pagination.Page, pagination.PageSize, totalCount);
    }
}
