using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using System.Text;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// 技能能力实现：维护技能定义列表，按需生成技能摘要提示词
    /// </summary>
    public sealed class SkillCapable : ISkilCapable
    {
        private readonly List<SkillDefinition> _skills = [];

        /// <inheritdoc />
        public IReadOnlyList<SkillDefinition> Skills => _skills;

        /// <inheritdoc />
        public void RegiSkill(SkillDefinition skill)
        {
            ArgumentNullException.ThrowIfNull(skill);

            // 按名称去重，避免重复注册
            if (!string.IsNullOrEmpty(skill.Name) &&
                _skills.Any(s => string.Equals(s.Name, skill.Name, StringComparison.OrdinalIgnoreCase)))
                return;

            _skills.Add(skill);
        }

        /// <inheritdoc />
        public string BuildSkillPropmt()
        {
            if (_skills.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("# 可用技能");
            sb.AppendLine("以下技能可通过 findskill 工具获取详细用法后调用：");

            foreach (var skill in _skills)
            {
                sb.AppendLine($"- **{skill.Name}**: {skill.Description}");
            }

            return sb.ToString();
        }
    }
}
