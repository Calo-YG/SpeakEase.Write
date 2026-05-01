namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class ContextFocus
{
    public List<string> CharacterIds { get; set; } = new();

    public List<string> CharacterNames { get; set; } = new();

    public List<string> LocationKeywords { get; set; } = new();

    public string? CurrentChapterId { get; set; }

    public int? MaxChapters { get; set; }

    public int? MaxCharacters { get; set; }
}
