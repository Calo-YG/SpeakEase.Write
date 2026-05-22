namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

// 上下文焦点配置：控制 Agent 构建上下文时加载的角色范围、章节窗口大小
public sealed class ContextFocus
{
    // 需要聚焦的角色 ID 列表
    public List<string> CharacterIds { get; set; } = new();

    // 需要聚焦的角色名称列表
    public List<string> CharacterNames { get; set; } = new();

    // 场景地点关键词，用于检索关联章节
    public List<string> LocationKeywords { get; set; } = new();

    // 当前编辑的章节 ID
    public string CurrentChapterId { get; set; }

    // 上下文窗口中最多加载的章节数
    public int MaxChapters { get; set; }

    // 上下文窗口中最多加载的角色数
    public int MaxCharacters { get; set; }
}
