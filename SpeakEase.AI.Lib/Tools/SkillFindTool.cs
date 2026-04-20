using Microsoft.Extensions.Hosting;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Tools
{
    /// <summary>
    /// 基于文件系统的技能发现工具（延迟加载）。
    /// 构造函数仅扫描目录并提取 YAML Front Matter 元数据，
    /// Markdown 正文（<see cref="SkillDefinition.SystemPrompt"/>）仅在调用 <see cref="GetSkill"/> 时按需加载。
    /// </summary>
    public sealed class SkillFindTool : ISkillCapable
    {
        private readonly Dictionary<string, SkillFileEntry> _skillFiles;

        /// <summary>
        /// 初始化技能发现工具，扫描文件系统并提取轻量元数据。
        /// </summary>
        /// <param name="hostEnvironment">主机环境，用于定位 ContentRootPath。</param>
        public SkillFindTool(IHostEnvironment hostEnvironment)
        {
            var basePath = Path.Combine(hostEnvironment.ContentRootPath, "wwwroot", "skills");
            _skillFiles = ScanSkillFiles(basePath);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 返回的列表仅包含技能的元数据（Name、Description），
        /// <see cref="SkillDefinition.SystemPrompt"/> 为空，避免上下文臃肿。
        /// 如需完整内容请调用 <see cref="GetSkill"/>。
        /// </remarks>
        public IReadOnlyList<SkillDefinition> Skills => _skillFiles
            .Select(kv => new SkillDefinition
            {
                Name = kv.Value.Name,
                Description = kv.Value.Description,
                SystemPrompt = null
            })
            .ToList();

        /// <inheritdoc />
        /// <remarks>
        /// 按需加载：重新读取对应 skill.md 的完整内容并解析 Markdown 正文。
        /// </remarks>
        public SkillDefinition GetSkill(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null!;

            if (!_skillFiles.TryGetValue(name, out var entry))
                return null!;

            try
            {
                var content = File.ReadAllText(entry.FilePath);
                return ParseSkillMd(content, entry.Name, entry.Description);
            }
            catch
            {
                return null!;
            }
        }

        /// <summary>
        /// 扫描目录，仅提取每个 skill.md 的 Front Matter 元数据，不加载正文。
        /// </summary>
        private static Dictionary<string, SkillFileEntry> ScanSkillFiles(string basePath)
        {
            var result = new Dictionary<string, SkillFileEntry>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(basePath))
                return result;

            try
            {
                var subDirs = Directory.GetDirectories(basePath);
                foreach (var subDir in subDirs)
                {
                    var skillMdPath = FindSkillMdFile(subDir);
                    if (skillMdPath is null)
                        continue;

                    try
                    {
                        // 仅读取 Front Matter 以获取元数据，避免加载正文
                        var frontMatter = ReadFrontMatter(skillMdPath);
                        var name = ExtractYamlValue(frontMatter, "name");

                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        var description = ExtractYamlValue(frontMatter, "description") ?? string.Empty;

                        result[name.Trim()] = new SkillFileEntry
                        {
                            FilePath = skillMdPath,
                            Name = name.Trim(),
                            Description = description.Trim()
                        };
                    }
                    catch
                    {
                        // 忽略单个文件解析错误
                    }
                }
            }
            catch
            {
                // 忽略目录扫描错误
            }

            return result;
        }

        /// <summary>
        /// 只读取文件的前若干行，直到找到 Front Matter 结束标记 <c>---</c> 为止。
        /// </summary>
        private static string ReadFrontMatter(string filePath)
        {
            using var reader = new StreamReader(filePath);
            var sb = new System.Text.StringBuilder();
            const int maxLines = 50;

            for (int i = 0; i < maxLines; i++)
            {
                var line = reader.ReadLine();
                if (line is null)
                    break;

                sb.AppendLine(line);

                // 遇到第二个 --- 表示 Front Matter 结束
                if (i > 0 && line.Trim() == "---")
                    break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 在指定目录中查找技能定义文件，兼容大小写：skill.md / SKILL.md / Skill.md。
        /// </summary>
        private static string FindSkillMdFile(string directory)
        {
            try
            {
                foreach (var file in Directory.GetFiles(directory, "*.md"))
                {
                    if (string.Equals(Path.GetFileName(file), "skill.md", StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        /// <summary>
        /// 完整解析 skill.md，包含 Markdown 正文（SystemPrompt）。
        /// </summary>
        private static SkillDefinition ParseSkillMd(string markdownContent, string fallbackName, string fallbackDescription)
        {
            if (string.IsNullOrWhiteSpace(markdownContent))
                return null!;

            var content = markdownContent.TrimStart();

            if (!content.StartsWith("---"))
                return null!;

            var endMarkerIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (endMarkerIndex < 0)
            {
                endMarkerIndex = content.IndexOf("\r\n---", 3, StringComparison.Ordinal);
                if (endMarkerIndex < 0)
                    return null!;
            }

            var bodyStart = content.IndexOf('\n', endMarkerIndex + 1);
            var body = bodyStart >= 0 ? content[(bodyStart + 1)..].Trim() : string.Empty;

            return new SkillDefinition
            {
                Name = fallbackName,
                Description = fallbackDescription,
                SystemPrompt = body
            };
        }

        /// <summary>
        /// 从简单 YAML 文本中提取指定 key 的值。
        /// </summary>
        private static string ExtractYamlValue(string yaml, string key)
        {
            foreach (var rawLine in yaml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                {
                    return line[(key.Length + 1)..].Trim();
                }
            }
            return null;
        }

        /// <summary>
        /// 技能文件条目，仅存储文件路径与轻量元数据。
        /// </summary>
        private sealed class SkillFileEntry
        {
            public string FilePath { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }
    }
}
