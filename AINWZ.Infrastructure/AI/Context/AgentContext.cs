using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Context;

public sealed class AgentContext
{
    public List<ChatMessage> ConversationHistory { get; set; } = new();

    public List<string> HistoryMessage { get; set; } = new();

    public string ProjectMemory { get; set; } = string.Empty;

    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    public string SnapshotId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public int InputTokenCount { get; set; }

    public int MemoryTokenCount { get; set; }

    public int RecentContextTokenCount { get; set; }

    public bool WasTrimmed { get; set; }
}
