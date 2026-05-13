using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.Write.Infrastructure.AI.Contract;

/// <summary>
/// 小说创作 Agent 契约。每个 Agent 是独立的 ReAct 编排单元，
/// 直接使用 IChatCompatible + IToolCapable 实现推理-行动循环。
/// </summary>
public interface INovelAgent
{
    string Name { get; }

    string DisplayName { get; }

    string BuildPrompt();

    void RegisterTools(IToolCapable toolCapable);

    IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        CancellationToken cancellationToken);

    AgentMetadata Metadata { get; }

    string RouteDescription { get; }
}
