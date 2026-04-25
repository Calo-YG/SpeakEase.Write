namespace SpeakEase.Write.Application.Contracts.Story.Dto;

/// <summary>
/// 角色列表项响应 DTO。
/// </summary>
public sealed class CharacterItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string AgeDescription { get; set; } = string.Empty;
    public string Identity { get; set; } = string.Empty;
    public string Appearance { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string BackgroundStory { get; set; } = string.Empty;
    public string Motivation { get; set; } = string.Empty;
    public string AbilityDescription { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 创建/更新角色请求 DTO。
/// </summary>
public sealed class SaveCharacterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Alias { get; set; }
    public string Gender { get; set; }
    public string AgeDescription { get; set; }
    public string Identity { get; set; }
    public string Appearance { get; set; }
    public string Personality { get; set; }
    public string BackgroundStory { get; set; }
    public string Motivation { get; set; }
    public string AbilityDescription { get; set; }
    public List<string> Tags { get; set; }
}
