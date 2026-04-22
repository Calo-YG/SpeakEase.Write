using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using System.Runtime.CompilerServices;

namespace SpeakEase.Write.Application.Applications;

/// <summary>
/// Agent 对话应用服务：通过 DI 注入的 IReActAgent 执行，模型配置由 IOpenAIContext 动态解析。
/// </summary>
public sealed class AgentApplication(IReActAgent reActAgent) : IAgentApplication
{
    /// <inheritdoc />
    public async Task<AgentResponse> ChatAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default)
    {
        return await reActAgent.ExecuteAsync(MapToAgentRequest(request), cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(
        AgentChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in reActAgent.ExecuteStreamAsync(MapToAgentRequest(request), cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// 将 DTO 映射为 AgentRequest
    /// </summary>
    private static AgentRequest MapToAgentRequest(AgentChatRequestDto dto)
    {
        var lastUserMsg = dto.Messages?.LastOrDefault(m => m.Role == "user");
        var history = new List<SpeakEase.AI.Lib.OpenAIModel.ChatMessage>();

        if (dto.Messages != null)
        {
            foreach (var msg in dto.Messages)
            {
                if (msg == lastUserMsg) continue;
                history.Add(msg.Role switch
                {
                    "system" => SpeakEase.AI.Lib.OpenAIModel.ChatMessage.System(msg.Content ?? string.Empty),
                    "assistant" => SpeakEase.AI.Lib.OpenAIModel.ChatMessage.Assistant(msg.Content ?? string.Empty),
                    _ => SpeakEase.AI.Lib.OpenAIModel.ChatMessage.User(msg.Content ?? string.Empty)
                });
            }
        }

        return new AgentRequest
        {
            Model = null, // 由 IOpenAIContext 动态解析
            Temperature = dto.Temperature,
            MaxTokens = dto.MaxTokens,
            MaxIterations = dto.MaxIterations,
            SkillName = dto.SkillName,
            UserMessage = lastUserMsg?.Content,
            ConversationHistory = history
        };
    }
}
