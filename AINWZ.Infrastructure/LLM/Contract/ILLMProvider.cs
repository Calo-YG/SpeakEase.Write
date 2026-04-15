using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM.Contract;

/// <summary>
/// LLM Provider 抽象。
/// </summary>
public interface ILLMProvider
{
    Task<LLMChatResponse> ChatAsync(LLMChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<LLMStreamEvent> StreamAsync(LLMChatRequest request, CancellationToken cancellationToken = default);
}
