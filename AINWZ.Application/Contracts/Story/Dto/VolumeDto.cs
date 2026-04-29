namespace SpeakEase.Write.Application.Contracts.Story.Dto;

public sealed class VolumeItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int ChapterCount { get; set; }
}

public sealed class CreateVolumeRequest
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int? Sequence { get; set; }
}

public sealed class UpdateVolumeRequest
{
    public string Title { get; set; }
    public string Summary { get; set; }
    public int? Sequence { get; set; }
}

public sealed class MergeVolumeRequest
{
    public string TargetVolumeId { get; set; } = string.Empty;
}
