using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.AI;

/// <summary>
/// LLM 调用日志查询应用服务接口。
/// </summary>
public interface ILLMCallLogApplication
{
    /// <summary>
    /// 分页查询调用日志。
    /// </summary>
    /// <param name="request">分页查询请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页结果。</returns>
    Task<ApiResult<PageResult<LLMCallLogDto>>> GetPagedAsync(LLMCallLogQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据标识获取单条调用日志。
    /// </summary>
    /// <param name="id">日志唯一标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>日志详情。</returns>
    Task<ApiResult<LLMCallLogDto>> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
