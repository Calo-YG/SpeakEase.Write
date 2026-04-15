using AINWZ.Infrastructure.LLM.LLM.Contract;

namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// 基于内存的 LLM 技能注册表。
/// </summary>
public sealed class InMemoryLLMSkillRegistry : ILLMSkillRegistry
{
    private readonly List<LLMSkillDefinition> _skills;

    /// <summary>
    /// 初始化内置技能。
    /// </summary>
    public InMemoryLLMSkillRegistry()
    {
        _skills = new List<LLMSkillDefinition>
        {
            new()
            {
                Name = "writer",
                Description = "适合长文、创作、润色与风格统一。",
                SystemPrompt = "你是专业中文写作助手。输出要求结构清晰、语言自然、避免空话，优先给出可直接使用的正文。"
            },
            new()
            {
                Name = "coder",
                Description = "适合代码实现、调试、重构与接口设计。",
                SystemPrompt = "你是资深软件工程助手。优先给出可执行、最小、正确的实现，避免脱离上下文的空泛建议。"
            },
            new()
            {
                Name = "analyst",
                Description = "适合分析、归纳、方案比较与结构化输出。",
                SystemPrompt = "你是严谨的分析助手。先抽取事实，再归纳结论，输出保持结构化、简洁且可落地。"
            }
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<LLMSkillDefinition> GetAll()
    {
        return _skills;
    }

    /// <inheritdoc />
    public LLMSkillDefinition GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _skills.FirstOrDefault(skill => string.Equals(skill.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
