namespace ClaudCare.Api.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public static class Paging
{
    public const int MaxPageSize = 50;
    public static (int page, int size) Clamp(int? page, int? size, int defaultSize = 20) =>
        (Math.Max(1, page ?? 1), Math.Clamp(size ?? defaultSize, 1, MaxPageSize));
}
