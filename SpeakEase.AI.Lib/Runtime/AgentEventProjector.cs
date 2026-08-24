using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// 将 Runtime Event 投影为兼容的 SSE Chunk。SSE 不参与运行状态管理。
/// </summary>
public sealed class AgentEventProjector
{
    public AgentStreamChunk ProjectToSse(AgentEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        var chunk = runtimeEvent.Chunk ?? new AgentStreamChunk
        {
            Type = runtimeEvent.Type,
            Content = runtimeEvent.Payload?.ToString() ?? string.Empty
        };
        chunk.RunId = runtimeEvent.RunId;
        chunk.StepId = runtimeEvent.StepId;
        chunk.Sequence = runtimeEvent.Sequence;
        return chunk;
    }
}
