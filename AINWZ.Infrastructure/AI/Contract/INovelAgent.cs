using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.Write.Infrastructure.AI.Contract;

/// <summary>
/// 所有小说创作 Agent 的统一接口。
/// 注意：INovelAgent 不是 IReActAgent 的子类型。
/// 各 Novel Agent 是独立的上层编排单元，内部组合 ReActAgent 作为 LLM 对话引擎来使用。
/// 每个 Agent 自己管理：Prompt 构建、工具注册、执行流程。
/// </summary>
public interface INovelAgent
{
    string Name { get; }

    string DisplayName { get; }

    /// <summary>
    /// 构建 System Prompt（不含黑板上下文，只含角色定义 + 写作规范 + 工具引导）
    /// </summary>
    string BuildPrompt();

    /// <summary>
    /// 注册该 Agent 专属的创作域工具到 IToolCapable
    /// </summary>
    void RegisterTools(IToolCapable toolCapable);

    /// <summary>
    /// 流式执行。
    /// 内部组合 ReActAgent 作为 LLM 对话引擎，但执行流程完全由本 Agent 控制。
    /// </summary>
    IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        CancellationToken cancellationToken);
}
