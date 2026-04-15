namespace AINWZ.Infrastructure.LLM.LLM.Contract;

/// <summary>
/// LLM 服务调用切片接口。
/// </summary>
public interface ILLMServiceFilter
{
    Task<LLMChatResponse> InvokeChatAsync(LLMChatRequest request, Func<LLMChatRequest, CancellationToken, Task<LLMChatResponse>> next, CancellationToken cancellationToken = default);

    IAsyncEnumerable<LLMStreamEvent> InvokeStreamAsync(LLMChatRequest request, Func<LLMChatRequest, CancellationToken, IAsyncEnumerable<LLMStreamEvent>> next, CancellationToken cancellationToken = default);
}
