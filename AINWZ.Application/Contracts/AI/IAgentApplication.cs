using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.Write.Application.Contracts.AI;

/// <summary>
/// Agent 对话应用服务接口
/// </summary>
public interface IAgentApplication
{
    /// <summary>
    /// 非流式 Agent 对话
    /// </summary>
    Task<AgentResponse> ChatAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式 Agent 对话
    /// </summary>
    IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default);
}