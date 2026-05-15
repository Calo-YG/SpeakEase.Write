namespace SpeakEase.Write.Application.Contracts.Story.Dto;

public sealed class CharacterGraphResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public string LayoutJson { get; set; } = string.Empty;
    public List<CharacterGraphNodeResponse> Nodes { get; set; } = new();
    public List<CharacterGraphEdgeResponse> Edges { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public sealed class CharacterGraphNodeResponse
{
    public string Id { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public int Importance { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public string StyleJson { get; set; } = string.Empty;
}

public sealed class CharacterGraphEdgeResponse
{
    public string Id { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Weight { get; set; }
    public string Direction { get; set; } = string.Empty;
}

public sealed class CharacterArcResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public int StageOrder { get; set; }
    public string StageTitle { get; set; } = string.Empty;
    public string InitialState { get; set; } = string.Empty;
    public string ChangedState { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
}

public sealed class SaveCharacterGraphRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; }
    public string LayoutJson { get; set; }
}

public sealed class UpdateGraphLayoutRequest
{
    public string LayoutJson { get; set; }
}

public sealed class SaveCharacterArcRequest
{
    public int StageOrder { get; set; }
    public string StageTitle { get; set; } = string.Empty;
    public string InitialState { get; set; } = string.Empty;
    public string ChangedState { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
}
