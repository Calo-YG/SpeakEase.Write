using SpeakEase.AI.Lib.Contract;

namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// 兼容现有 ISkilCapable 的 Runtime 适配器。详细正文仍由 find_skill 工具读取。
/// </summary>
public sealed class LegacySkillResolverAdapter(ISkilCapable skills) : ISkillResolver
{
    public async Task<SkillContent> ResolveAsync(
        string skillName,
        CancellationToken cancellationToken = default)
    {
        var definition = skills.Skills.FirstOrDefault(x =>
            string.Equals(x.Name, skillName, StringComparison.OrdinalIgnoreCase));

        if (definition is null)
            return new SkillContent { SkillName = skillName ?? string.Empty };

        var content = definition.Description ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(definition.Path))
        {
            var path = Path.GetFullPath(definition.Path);
            if (File.Exists(path))
                content = await File.ReadAllTextAsync(path, cancellationToken);
        }

        return new SkillContent
        {
            SkillName = definition.Name,
            Path = definition.Path,
            Content = content
        };
    }
}
