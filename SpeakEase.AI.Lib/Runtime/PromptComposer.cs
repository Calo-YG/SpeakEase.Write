namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// 按稳定顺序将 PromptProfile 组合为系统提示词。
/// </summary>
public sealed class PromptComposer
{
    public string Compose(PromptProfile profile, PromptCompositionContext context = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var sections = new List<(string Title, string Content)>();
        AddText(sections, "身份", profile.Identity);
        AddText(sections, "任务目标", string.IsNullOrWhiteSpace(context?.TaskObjective)
            ? profile.Objective
            : context.TaskObjective);
        AddList(sections, "用户约束", context?.UserConstraints);
        AddList(sections, "质量标准", profile.QualityCriteria);
        AddList(sections, "风格提示", profile.StyleHints);
        AddText(sections, "相关上下文", context?.ContextSummary);
        AddList(sections, "可用能力", context?.Capabilities);
        AddText(sections, "输出契约", profile.OutputContract);

        return string.Join("\n\n", sections.Select(x => $"# {x.Title}\n{x.Content}"));
    }

    private static void AddText(List<(string Title, string Content)> sections, string title, string content)
    {
        if (!string.IsNullOrWhiteSpace(content))
            sections.Add((title, content.Trim()));
    }

    private static void AddList(List<(string Title, string Content)> sections, string title, IEnumerable<string> values)
    {
        var items = values?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => $"- {x.Trim()}")
            .ToList() ?? new List<string>();

        if (items.Count > 0)
            sections.Add((title, string.Join("\n", items)));
    }
}
