namespace SpeakEase.Write.Application.Shared;

public sealed class Pagination
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string Order { get; set; } = "CreatedAt";
    public string OrderBy { get; set; } = "desc";
}
