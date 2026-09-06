namespace SpeakEase.Write.Application.Shared;

public class PageResult<T>
{
    public int TotalCount { get; set; }
    public List<T> Items { get; set; } = new();
    public int PageIndex { get; set; }
    public int PageSize { get; set; }

    public static PageResult<T> Create(int totalCount, List<T> items, int pageIndex, int pageSize)
    {
        return new PageResult<T>
        {
            TotalCount = totalCount,
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
}
