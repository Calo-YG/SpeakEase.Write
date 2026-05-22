using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.Write.Infrastructure.AI.Contract;

/// <summary>
/// 小说创作 Agent 契约。每个 Agent 是独立的 ReAct 编排单元，
/// 直接使用 IChatCompatible + IToolCapable 实现推理-行动循环。
/// </summary>
// 所有小说创作Agent必须实现此接口，定义Agent的基本能力：名称、提示词、工具注册、流式执行
public interface INovelAgent
{
    string Name { get; } // Agent唯一标识

    string DisplayName { get; } // Agent显示名称

    string BuildPrompt(); // 构建系统提示词

    void RegisterTools(IToolCapable toolCapable); // 注册Agent所需的工具

    // 流式执行ReAct循环，返回AgentStreamChunk流（content / reasoning / tool_call / tool_result / done / error）
    IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        CancellationToken cancellationToken);

    AgentMetadata Metadata { get; } // Agent元数据配置

    string RouteDescription { get; } // Agent功能描述，用于路由分发
}
