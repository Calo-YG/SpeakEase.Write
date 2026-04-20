using SpeakEase.AI.Lib.Models;
namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent 技能注册能力接口（可选）。
    /// 实现此接口的 Agent 可以独立管理自己的技能集。
    /// </summary>
    public interface ISkillCapable
    {
        /// <summary>
        /// 该 Agent 已注册的技能列表。
        /// </summary>
        IReadOnlyList<SkillDefinition> Skills { get; }

        /// <summary>
        /// 按名称获取技能。
        /// </summary>
        SkillDefinition GetSkill(string name);
    }
}
