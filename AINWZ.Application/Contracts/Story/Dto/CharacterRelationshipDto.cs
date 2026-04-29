namespace SpeakEase.Write.Application.Contracts.Story.Dto;

public sealed class CharacterRelationshipResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string SourceCharacterId { get; set; } = string.Empty;
    public string TargetCharacterId { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Intensity { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveCharacterRelationshipRequest
{
    public string SourceCharacterId { get; set; } = string.Empty;
    public string TargetCharacterId { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public string Description { get; set; }
    public int? Intensity { get; set; }
}
