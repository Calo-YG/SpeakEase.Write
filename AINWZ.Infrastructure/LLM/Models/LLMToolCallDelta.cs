namespace AINWZ.Infrastructure.LLM.Models;

/// <summary>
/// 流式工具调用增量。
/// </summary>
public sealed class LLMToolCallDelta
{
    public int Index { get; set; }

    public string Id { get; set; }

    public string Type { get; set; }

    public string Name { get; set; }

    public string Arguments { get; set; }
}
