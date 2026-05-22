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

        /// <summary>
        /// 缓存的技能提示词，注册新技能时失效
        /// </summary>
        private string _cachedPrompt;

        /// <inheritdoc />
        public IReadOnlyList<SkillDefinition> Skills => _skills;

        /// <inheritdoc />
        public void RegiSkill(SkillDefinition skill)
        {
            ArgumentNullException.ThrowIfNull(skill);

            // 按名称去重：同名技能（忽略大小写）不重复注册
            if (!string.IsNullOrEmpty(skill.Name) &&
                _skills.Any(s => string.Equals(s.Name, skill.Name, StringComparison.OrdinalIgnoreCase)))
                return;

            _skills.Add(skill);
            // 新技能注册后，缓存的提示词失效，下次 BuildSkillPropmt 时重建
            _cachedPrompt = null;
        }

        /// <inheritdoc />
        public string BuildSkillPropmt()
        {
            // 无技能时返回空
            if (_skills.Count == 0)
                return string.Empty;

            // 有缓存直接返回，避免重复构建字符串
            if (_cachedPrompt != null)
                return _cachedPrompt;

            _cachedPrompt = BuildSkillPromptCore();
            return _cachedPrompt;
        }

        /// <summary>
        /// 实际构建技能提示词的逻辑：生成 Markdown 格式的技能列表
        /// </summary>
        private string BuildSkillPromptCore()
        {
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
