namespace SpeakEase.Write.Domain.Entities.AI;

public class AICreationSessionEntity : AggregateRootEntity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string WorkId { get; set; } = string.Empty;

    /// <summary>active | paused | closed | cancelled | expired</summary>
    public string Status { get; set; } = "active";

    public int TurnCount { get; set; }

    /// <summary>最后一轮的黑板上下文快照 JSON</summary>
    public string ContextSnapshotJson { get; set; } = string.Empty;

    /// <summary>已采纳的内容列表 JSON</summary>
    public string AdoptedContentJson { get; set; } = string.Empty;

    /// <summary>对话历史 JSON</summary>
    public string MessagesJson { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    /// <summary>会话终止原因（取消/错误时填写）</summary>
    public string CloseReason { get; set; } = string.Empty;
}
