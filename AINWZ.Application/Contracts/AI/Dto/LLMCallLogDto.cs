namespace SpeakEase.Write.Application.Contracts.AI.Dto;

/// <summary>
/// LLM 调用日志响应 DTO。
/// </summary>
public sealed class LLMCallLogDto
{
    /// <summary>
    /// 日志唯一标识。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 调用类型，如 chat 或 stream。
    /// </summary>
    public string CallType { get; set; } = string.Empty;

    /// <summary>
    /// 命中的技能名称。
    /// </summary>
    public string SkillName { get; set; }

    /// <summary>
    /// 请求摘要。
    /// </summary>
    public string RequestSummary { get; set; } = string.Empty;

    /// <summary>
    /// 响应摘要。
    /// </summary>
    public string ResponseSummary { get; set; }

    /// <summary>
    /// 首选模型标识。
    /// </summary>
    public string PrimaryModel { get; set; }

    /// <summary>
    /// 最终命中模型标识。
    /// </summary>
    public string FinalModel { get; set; }

    /// <summary>
    /// 是否命中了备用模型。
    /// </summary>
    public bool UsedFallback { get; set; }

    /// <summary>
    /// 备用模型标识。
    /// </summary>
    public string FallbackModel { get; set; }

    /// <summary>
    /// Provider 请求标识。
    /// </summary>
    public string RequestId { get; set; }

    /// <summary>
    /// 模型结束原因。
    /// </summary>
    public string FinishReason { get; set; }

    /// <summary>
    /// 工具调用摘要。
    /// </summary>
    public string ToolCallsSummary { get; set; }

    /// <summary>
    /// 工具执行结果摘要。
    /// </summary>
    public string ToolResultsSummary { get; set; }

    /// <summary>
    /// 是否调用成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误信息摘要。
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    public DateTime CreateAt { get; set; }
}
