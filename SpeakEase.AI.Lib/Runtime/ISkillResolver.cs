namespace SpeakEase.AI.Lib.Runtime;

public interface ISkillResolver
{
    Task<SkillContent> ResolveAsync(
        string skillName,
        CancellationToken cancellationToken = default);
}

public sealed class SkillContent
{
    public string SkillName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}
