namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// 技能定义，包含预设的系统提示词，可动态注入 Agent 上下文
/// </summary>
public sealed class SkillDefinition
{
    /// <summary>
    /// 技能名称（用于展示和匹配）
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 技能功能描述（注入到 SystemPrompt 供 LLM 了解可用技能）
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 技能文档路径（指向 SKILL.md，SkillFindTool 据此读取完整文档）
    /// </summary>
    public string Path { get; set; }
}
