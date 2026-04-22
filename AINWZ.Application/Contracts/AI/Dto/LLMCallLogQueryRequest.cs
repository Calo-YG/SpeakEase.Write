using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.AI.Dto;

/// <summary>
/// LLM 调用日志分页查询请求。
/// </summary>
public sealed class LLMCallLogQueryRequest
{
    /// <summary>
    /// 分页参数。
    /// </summary>
    public Pagination Pagination { get; set; } = new();

    /// <summary>
    /// 调用类型过滤（可选），如 chat 或 stream。
    /// </summary>
    public string CallType { get; set; }

    /// <summary>
    /// 技能名称过滤（可选）。
    /// </summary>
    public string SkillName { get; set; }

    /// <summary>
    /// 是否只查询失败的调用（可选）。
    /// </summary>
    public bool? OnlyFailed { get; set; }
}
