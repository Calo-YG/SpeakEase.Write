namespace SpeakEase.Write.Infrastructure.AI.Memory;

// 会话记忆提供者接口：管理长篇写作中 Agent 上下文的持久化记忆快照
public interface IMemoryProvider
{
    // 加载指定会话的记忆快照（项目摘要 + 历史摘要）
    Task<SessionMemorySnapshot> LoadSessionMemoryAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken = default);

    // 每轮对话后刷新记忆快照（累计压缩历史）
    Task RefreshAfterTurnAsync(
        string userId,
        string workId,
        string sessionId,
        int turnNumber,
        CancellationToken cancellationToken = default);

    // 使指定会话的缓存失效
    Task InvalidateSessionAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken = default);

    // 预热：将作品级别的记忆数据加载到多级缓存
    Task LoadAsync(string userId, string workId, CancellationToken cancellationToken = default);
    // 持久化当前缓存快照到数据库
    Task SaveSnapshotAsync(string userId, string workId, CancellationToken cancellationToken = default);
    // 使作品级别的缓存失效
    Task InvalidateAsync(string userId, string workId, CancellationToken cancellationToken = default);
}

// 会话记忆快照：包含压缩后的摘要信息，注入到 Agent 系统提示中
public sealed class SessionMemorySnapshot
{
    public static SessionMemorySnapshot Empty => new();

    // 快照唯一标识
    public string SnapshotId { get; set; } = string.Empty;

    // 压缩后的对话摘要文本
    public string Summary { get; set; } = string.Empty;

    // 完整 JSON 快照（调试用）
    public string SnapshotJson { get; set; } = string.Empty;

    // 当前对话轮次
    public int TurnNumber { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // 是否存在有效快照
    public bool HasSnapshot => !string.IsNullOrWhiteSpace(SnapshotId);
}
