namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class WritingBlackboard
{
    public string WorkId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;

    public WorldSettingSection WorldSetting { get; set; } = new();

    public OutlineSection Outline { get; set; } = new();

    public List<CharacterSection> Characters { get; set; } = new();

    public List<ChapterSection> RecentChapters { get; set; } = new();

    public List<AuditResultSection> AuditResults { get; set; } = new();

    public WritingMetaInfo Meta { get; set; } = new();
}

public sealed class WorldSettingSection
{
    public string WorldRules { get; set; } = string.Empty;
    public string Geography { get; set; } = string.Empty;
    public string Factions { get; set; } = string.Empty;
    public string History { get; set; } = string.Empty;
    public DateTime LastUpdatedAt { get; set; }
}

public sealed class OutlineSection
{
    public List<VolumeNode> Volumes { get; set; } = new();
    public string OverallArc { get; set; } = string.Empty;
    public DateTime LastUpdatedAt { get; set; }
}

public sealed class VolumeNode
{
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<ChapterNode> Chapters { get; set; } = new();
}

public sealed class ChapterNode
{
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = "outline";
}

public sealed class CharacterSection
{
    public string CharacterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CoreSeed { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string Traits { get; set; } = string.Empty;
    public string Voice { get; set; } = string.Empty;
    public string Arc { get; set; } = string.Empty;
    public List<string> Relationships { get; set; } = new();
    public List<string> Fears { get; set; } = new();
    public List<string> Desires { get; set; } = new();
    public DateTime LastGrowthAt { get; set; }
}

public sealed class ChapterSection
{
    public string ChapterId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class AuditResultSection
{
    public string CheckType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
}

public sealed class WritingMetaInfo
{
    public string Genre { get; set; } = string.Empty;
    public string Perspective { get; set; } = string.Empty;
    public List<string> StyleTags { get; set; } = new();
    public int TotalWordCount { get; set; }
    public string CurrentFocus { get; set; } = string.Empty;
    public string PreferredModel { get; set; } = string.Empty;
}
