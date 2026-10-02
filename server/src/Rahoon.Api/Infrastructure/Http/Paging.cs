using Microsoft.EntityFrameworkCore;

namespace Rahoon.Api.Infrastructure.Http;

/// <summary>
/// Server pagination for lists: page from 1, page size clamped (default 25, max 100). The caller orders the query with a
/// deterministic last key (the id) so pages never overlap. Answers <c>{ items, total, page, pageSize, pages }</c>.
/// </summary>
public static class Paging
{
    public const int DefaultSize = 25;
    public const int MaxSize = 100;

    public static (int Page, int Size) Normalize(int? page, int? pageSize) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? DefaultSize, 1, MaxSize));

    public static async Task<(List<T> Rows, int Total, int Page, int Size)> PageAsync<T>(IQueryable<T> ordered, int? page, int? pageSize)
    {
        var (p, size) = Normalize(page, pageSize);
        var total = await ordered.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        p = Math.Min(p, pages);
        var rows = await ordered.Skip((p - 1) * size).Take(size).ToListAsync();
        return (rows, total, p, size);
    }

    public static object Result<T>(IEnumerable<T> items, int total, int page, int size) =>
        new { items, total, page, pageSize = size, pages = Math.Max(1, (int)Math.Ceiling(total / (double)size)) };
}
