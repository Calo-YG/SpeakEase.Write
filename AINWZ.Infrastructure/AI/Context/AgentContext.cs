using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Context;

// Agent 上下文：封装 LLM 调用所需的完整上下文信息
public sealed class AgentContext
{
    // 构建好的 ChatMessage 列表（系统提示 + 历史消息），直接发送给 LLM
    public List<ChatMessage> ConversationHistory { get; set; } = new();

    // 格式化的人类可读历史消息（日志/调试用）
    public List<string> HistoryMessage { get; set; } = new();

    // 项目级别记忆文本（作品摘要、角色概要等），注入为系统消息
    public string ProjectMemory { get; set; } = string.Empty;

    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    // 记忆快照 ID
    public string SnapshotId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    // 最终输入 LLM 的 token 数（裁剪后）
    public int InputTokenCount { get; set; }

    // 项目记忆占用的 token 数
    public int MemoryTokenCount { get; set; }

    // 最近对话历史占用的 token 数
    public int RecentContextTokenCount { get; set; }

    // 是否因超出 token 预算而裁剪了历史消息
    public bool WasTrimmed { get; set; }
}
