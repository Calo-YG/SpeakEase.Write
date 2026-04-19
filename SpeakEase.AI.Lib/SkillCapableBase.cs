using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// ISkillCapable 的基础实现。
    /// 管理技能的注册、查询与列表访问。
    /// </summary>
    public class SkillCapableBase : ISkillCapable
    {
        private readonly List<SkillDefinition> _skills = new();

        /// <inheritdoc />
        public IReadOnlyList<SkillDefinition> Skills => _skills.AsReadOnly();

        /// <inheritdoc />
        public void RegisterSkill(SkillDefinition skill)
        {
            if (string.IsNullOrWhiteSpace(skill.Name))
            {
                throw new ArgumentException("技能名称不能为空。");
            }

            // 同名技能覆盖
            var existingIndex = _skills.FindIndex(s => string.Equals(s.Name, skill.Name, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                _skills[existingIndex] = skill;
            }
            else
            {
                _skills.Add(skill);
            }
        }

        /// <inheritdoc />
        public SkillDefinition GetSkill(string name)
        {
            return _skills.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 移除指定名称的技能注册。
        /// </summary>
        public bool UnregisterSkill(string name)
        {
            return _skills.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }
}
