using SpeakEase.AI.Lib.Contract;

namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// 兼容现有 ISkilCapable 的 Runtime 适配器。详细正文仍由 find_skill 工具读取。
/// </summary>
public sealed class LegacySkillResolverAdapter(ISkilCapable skills) : ISkillResolver
{
    public Task<SkillContent> ResolveAsync(
        string skillName,
        CancellationToken cancellationToken = default)
    {
        var definition = skills.Skills.FirstOrDefault(x =>
            string.Equals(x.Name, skillName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(definition is null
            ? new SkillContent { SkillName = skillName ?? string.Empty }
            : new SkillContent
            {
                SkillName = definition.Name,
                Path = definition.Path,
                Content = definition.Description ?? string.Empty
            });
    }
}
