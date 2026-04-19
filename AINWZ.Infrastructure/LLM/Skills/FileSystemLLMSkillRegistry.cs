using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using AINWZ.Infrastructure.LLM.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AINWZ.Infrastructure.LLM.Skills;

/// <summary>
/// 基于文件系统的 LLM 技能注册表。
/// 扫描 BasePath 下所有子文件夹中的 skill.md，解析 YAML Front Matter + Markdown 正文，
/// 自动注册为 LLMSkillDefinition。若文件系统未找到任何 skill 且 FallbackToBuiltin=true，
/// 则回退到 InMemoryLLMSkillRegistry 的内置默认技能。
/// </summary>
public sealed class FileSystemLLMSkillRegistry : ILLMSkillRegistry
{
    private readonly Dictionary<string, LLMSkillDefinition> _skillsByName;
    private readonly List<LLMSkillDefinition> _skillsList;
    private readonly ILogger<FileSystemLLMSkillRegistry> _logger;

    public FileSystemLLMSkillRegistry(
        IOptions<SkillOptions> options,
        ILogger<FileSystemLLMSkillRegistry> logger,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        var skillOptions = options.Value;

        var basePath = Path.IsPathRooted(skillOptions.BasePath)
            ? skillOptions.BasePath
            : Path.Combine(hostEnvironment.ContentRootPath, skillOptions.BasePath);

        _logger.LogInformation("FileSystemLLMSkillRegistry 初始化: BasePath={BasePath}", basePath);

        var loaded = LoadFromDirectory(basePath);

        // 若文件系统未找到任何 skill，回退到内置默认技能
        if (loaded.Count == 0 && skillOptions.FallbackToBuiltin)
        {
            _logger.LogInformation("未从文件系统加载到任何 skill，回退到内置默认技能。");
            var fallback = new InMemoryLLMSkillRegistry();
            loaded = fallback.GetAll().ToList();
        }

        _skillsList = loaded;
        _skillsByName = loaded
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .ToDictionary(
                s => s.Name!,
                s => s,
                StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("FileSystemLLMSkillRegistry 加载完成: 共 {Count} 个技能 [{Skills}]",
            _skillsList.Count, string.Join(", ", _skillsList.Select(s => s.Name)));
    }

    /// <inheritdoc />
    public IReadOnlyList<LLMSkillDefinition> GetAll() => _skillsList;

    /// <inheritdoc />
    public LLMSkillDefinition GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _skillsByName.TryGetValue(name, out var skill) ? skill : null;
    }

    /// <summary>
    /// 从指定目录扫描所有子文件夹中的 skill.md 文件。
    /// </summary>
    private List<LLMSkillDefinition> LoadFromDirectory(string basePath)
    {
        var results = new List<LLMSkillDefinition>();

        if (!Directory.Exists(basePath))
        {
            _logger.LogWarning("Skills 目录不存在: {BasePath}", basePath);
            return results;
        }

        try
        {
            var subDirs = Directory.GetDirectories(basePath);
            foreach (var subDir in subDirs)
            {
                // 兼容大小写：skill.md / SKILL.md / Skill.md
                var skillMdPath = FindSkillMdFile(subDir);
                if (skillMdPath is null) continue;

                try
                {
                    var content = File.ReadAllText(skillMdPath);
                    var definition = SkillMdParser.Parse(content, _logger as ILogger);
                    if (definition is not null)
                    {
                        results.Add(definition);
                        _logger.LogDebug("从 {Path} 加载技能: {Name}", skillMdPath, definition.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "读取 skill.md 失败: {Path}", skillMdPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫描 Skills 目录失败: {BasePath}", basePath);
        }

        return results;
    }

    /// <summary>
    /// 在指定目录中查找技能定义文件，兼容大小写：skill.md / SKILL.md / Skill.md。
    /// </summary>
    private static string FindSkillMdFile(string directory)
    {
        const string targetFileName = "skill.md";
        try
        {
            foreach (var file in Directory.GetFiles(directory, "*.md"))
            {
                if (string.Equals(Path.GetFileName(file), targetFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
