namespace backend.DTOs.Common;

public sealed class PagedResultDto<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; }
}