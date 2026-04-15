using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM.Contract;

/// <summary>
/// LLM 服务抽象，供应用层发起统一对话请求。
/// </summary>
public interface ILLMService
{
    /// <summary>
    /// 执行一次对话请求。
    /// </summary>
    Task<LLMChatResponse> ChatAsync(LLMChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以流式方式执行对话请求。
    /// </summary>
    IAsyncEnumerable<LLMStreamEvent> StreamAsync(LLMChatRequest request, CancellationToken cancellationToken = default);
}
