using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// Agent 运行时的稳定边界。上层消费事件，不依赖 ReAct 或具体 Agent 类型。
/// </summary>
public interface IAgentLoop
{
    IAsyncEnumerable<AgentEvent> RunAsync(
        AgentLoopRequest request,
        CancellationToken cancellationToken = default);
}
