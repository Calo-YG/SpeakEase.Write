namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// 技能定义，包含预设的系统提示词，可动态注入 Agent 上下文
/// </summary>
public sealed class SkillDefinition
{
    /// <summary>
    /// 技能名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string Path { get; set; }
}
