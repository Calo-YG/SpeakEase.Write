namespace SpeakEase.Write.Application.Contracts.Creation.Dto;

public sealed class CreationSessionDto
{
    public string SessionId { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TurnCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string CloseReason { get; set; } = string.Empty;
}

public sealed class AdoptContentRequest
{
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class AdoptedItem
{
    public int TurnNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime AdoptedAt { get; set; }
}
