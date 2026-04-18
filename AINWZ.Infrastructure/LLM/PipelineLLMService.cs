using AINWZ.Infrastructure.LLM;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using Microsoft.Extensions.Logging;


namespace AINWZ.Application.LLM;

/// <summary>
/// 基于 filter 管道的 LLM 服务实现。
/// </summary>
/// <remarks>
/// 初始化管道服务。
/// </remarks>
public sealed class PipelineLLMService(LLMService coreService, IEnumerable<ILLMServiceFilter> filters, ILogger<PipelineLLMService> logger) : ILLMService
{
    private readonly LLMService _coreService = coreService;
    private readonly IReadOnlyList<ILLMServiceFilter> _filters = filters.ToList();
    private readonly ILogger<PipelineLLMService> _logger = logger;

    /// <inheritdoc />
    public Task<LLMChatResponse> ChatAsync(LLMChatRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PipelineLLMService.ChatAsync: Filters={FilterCount}, Skill={Skill}", _filters.Count, request.SkillName ?? "(none)");

        Task<LLMChatResponse> Next(LLMChatRequest currentRequest, CancellationToken token)
        {
            return _coreService.ChatAsync(currentRequest, token);
        }

        var pipeline = _filters
            .Reverse()
            .Aggregate(
                (Func<LLMChatRequest, CancellationToken, Task<LLMChatResponse>>)Next,
                (next, filter) => (currentRequest, token) =>
                {
                    _logger.LogDebug("PipelineLLMService.ChatAsync 执行过滤器: {FilterType}", filter.GetType().Name);
                    return filter.InvokeChatAsync(currentRequest, next, token);
                });

        return pipeline(request, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LLMStreamEvent> StreamAsync(LLMChatRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PipelineLLMService.StreamAsync: Filters={FilterCount}, Skill={Skill}", _filters.Count, request.SkillName ?? "(none)");

        IAsyncEnumerable<LLMStreamEvent> Next(LLMChatRequest currentRequest, CancellationToken token)
        {
            return _coreService.StreamAsync(currentRequest, token);
        }

        var pipeline = _filters
            .Reverse()
            .Aggregate(
                (Func<LLMChatRequest, CancellationToken, IAsyncEnumerable<LLMStreamEvent>>)Next,
                (next, filter) => (currentRequest, token) =>
                {
                    _logger.LogDebug("PipelineLLMService.StreamAsync 执行过滤器: {FilterType}", filter.GetType().Name);
                    return filter.InvokeStreamAsync(currentRequest, next, token);
                });

        return pipeline(request, cancellationToken);
    }
}
