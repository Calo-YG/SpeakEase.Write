namespace SpeakEase.Write.Infrastructure.AI.Memory;

public sealed class MemoryContext
{
    public string WorkTitle { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Perspective { get; set; } = string.Empty;
    public int TotalWordCount { get; set; }
    public string WorkSummary { get; set; } = string.Empty;

    public List<MemoryChapter> RecentChapters { get; set; } = new();
    public List<MemoryCharacter> Characters { get; set; } = new();
    public List<MemoryOutlineNode> OutlineNodes { get; set; } = new();
    public string WorldSettingSummary { get; set; } = string.Empty;
    public List<MemoryForeshadowing> ActiveForeshadowings { get; set; } = new();
    public List<MemoryTimelineEvent> TimelineEvents { get; set; } = new();
}

public sealed class MemoryChapter
{
    public string Title { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class MemoryCharacter
{
    public string Name { get; set; } = string.Empty;
    public string Identity { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string RoleSummary { get; set; } = string.Empty;
}

public sealed class MemoryOutlineNode
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string ChapterId { get; set; } = string.Empty;
}

public sealed class MemoryForeshadowing
{
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
}

public sealed class MemoryTimelineEvent
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
}
