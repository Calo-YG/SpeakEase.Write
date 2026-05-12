namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// LLM 交互上下文：ReAct 循环内每次迭代不变的配置
/// </summary>
public sealed class LLMTurnContext
{
    /// <summary>
    /// 当前模型
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// 温度
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// 最大tokens
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Top-P 核采样，取值范围 (0, 1]。值越小输出越聚焦。
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// Presence Penalty，取值范围 [-2, 2]。正值鼓励模型谈论新话题。
    /// </summary>
    public double? PresencePenalty { get; set; }

    /// <summary>
    /// Frequency Penalty，取值范围 [-2, 2]。正值降低模型逐字重复已出现内容的概率。
    /// </summary>
    public double? FrequencyPenalty { get; set; }

    /// <summary>
    /// 工具选择策略，默认 auto。参见 <see cref="OpenAIModel.ToolChoice"/> 的静态常量。
    /// </summary>
    public object ToolChoice { get; set; } = OpenAIModel.ToolChoice.Auto;
}
