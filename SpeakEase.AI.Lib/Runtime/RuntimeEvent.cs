using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Runtime;

public sealed class RuntimeEvent
{
    public string RunId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string Type { get; init; } = string.Empty;
    public object Payload { get; init; }
    public AgentStreamChunk Chunk => Payload as AgentStreamChunk;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
