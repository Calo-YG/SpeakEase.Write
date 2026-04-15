namespace AINWZ.Infrastructure.LLM.LLM.Contract;

/// <summary>
/// LLM 技能注册表。
/// </summary>
public interface ILLMSkillRegistry
{
    IReadOnlyList<LLMSkillDefinition> GetAll();

    LLMSkillDefinition GetByName(string name);
}
