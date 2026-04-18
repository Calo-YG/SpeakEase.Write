namespace AINWZ.Infrastructure.LLM.Options;

/// <summary>
/// Skills 文件系统加载配置。
/// </summary>
public sealed class SkillOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "Skills";

    /// <summary>
    /// Skills 文件根目录（相对于 ContentRootPath）；默认 wwwroot/skills。
    /// </summary>
    public string BasePath { get; set; } = "wwwroot/skills";

    /// <summary>
    /// 当文件系统中未找到任何 skill 时，是否回退到内置默认技能；默认 true。
    /// </summary>
    public bool FallbackToBuiltin { get; set; } = true;
}
