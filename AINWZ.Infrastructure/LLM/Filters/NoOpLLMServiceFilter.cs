using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM.Filters;

/// <summary>
/// 空操作 LLM 切片。
/// </summary>
public sealed class NoOpLLMServiceFilter : ILLMServiceFilter
{
    public Task<LLMChatResponse> InvokeChatAsync(LLMChatRequest request, Func<LLMChatRequest, CancellationToken, Task<LLMChatResponse>> next, CancellationToken cancellationToken = default)
    {
        return next(request, cancellationToken);
    }

    public IAsyncEnumerable<LLMStreamEvent> InvokeStreamAsync(LLMChatRequest request, Func<LLMChatRequest, CancellationToken, IAsyncEnumerable<LLMStreamEvent>> next, CancellationToken cancellationToken = default)
    {
        return next(request, cancellationToken);
    }
}
