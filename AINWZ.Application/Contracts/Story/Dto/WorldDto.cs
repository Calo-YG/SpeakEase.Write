namespace SpeakEase.Write.Application.Contracts.Story.Dto;

public sealed class WorldSettingResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public string EraBackground { get; set; } = string.Empty;
    public string OverallStyle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveWorldSettingRequest
{
    public string WorldName { get; set; } = string.Empty;
    public string EraBackground { get; set; }
    public string OverallStyle { get; set; }
    public string Summary { get; set; }
    public string JsonContent { get; set; }
}

public sealed class GeographyResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GeographyType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParentGeographyId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveGeographyRequest
{
    public string Name { get; set; } = string.Empty;
    public string GeographyType { get; set; }
    public string Description { get; set; }
    public string ParentGeographyId { get; set; }
}

public sealed class FactionResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FactionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RelationshipJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveFactionRequest
{
    public string Name { get; set; } = string.Empty;
    public string FactionType { get; set; }
    public string Description { get; set; }
    public string RelationshipJson { get; set; }
}

public sealed class PowerSystemResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LevelDefinitionJson { get; set; } = string.Empty;
    public string AbilityRule { get; set; } = string.Empty;
    public string ResourceSystem { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class SavePowerSystemRequest
{
    public string Name { get; set; } = string.Empty;
    public string LevelDefinitionJson { get; set; }
    public string AbilityRule { get; set; }
    public string ResourceSystem { get; set; }
}

public sealed class WorldRuleResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ConstraintJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveWorldRuleRequest
{
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; }
    public string Description { get; set; }
    public string ConstraintJson { get; set; }
}

public sealed class HistoricalEventResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EraLabel { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public string ImpactSummary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveHistoricalEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; }
    public string EraLabel { get; set; }
    public DateTime? EventTime { get; set; }
    public string ImpactSummary { get; set; }
}
