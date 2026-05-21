namespace SpeakEase.Write.Domain.Entities.AI;

public class AICreationMessageEntity : Entity
{
    public string SessionId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int TurnNumber { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public bool? ToolSuccess { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
