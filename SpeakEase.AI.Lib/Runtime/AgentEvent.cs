using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// AgentLoop 产生的可审计运行事件。Payload 保持为兼容事件对象，SSE 层可按 Type 投影。
/// </summary>
public sealed class AgentEvent
{
    public string RunId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string Type { get; init; } = string.Empty;
    public object Payload { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public AgentStreamChunk Chunk => Payload as AgentStreamChunk;
}
