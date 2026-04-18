using AINWZ.Infrastructure.LLM.Models;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace AINWZ.Infrastructure.LLM.Skills;

/// <summary>
/// 解析 skill.md 文件：YAML Front Matter（name/description/defaultTools）+ Markdown 正文（systemPrompt）。
/// </summary>
public static class SkillMdParser
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// 解析 skill.md 内容为 LLMSkillDefinition。
    /// </summary>
    /// <param name="markdownContent">skill.md 文件的完整文本内容。</param>
    /// <param name="logger">可选日志记录器。</param>
    /// <returns>解析后的 LLMSkillDefinition；解析失败返回 null。</returns>
    public static LLMSkillDefinition Parse(string markdownContent, ILogger logger = null)
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            return null;
        }

        var content = markdownContent.TrimStart();

        // 检查 YAML Front Matter 起始标记
        if (!content.StartsWith("---"))
        {
            logger?.LogWarning("skill.md 缺少 YAML Front Matter 起始标记 '---'，跳过解析。");
            return null;
        }

        // 找到结束标记
        var endMarkerIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endMarkerIndex < 0)
        {
            // 尝试 \r\n--- 格式
            endMarkerIndex = content.IndexOf("\r\n---", 3, StringComparison.Ordinal);
            if (endMarkerIndex < 0)
            {
                logger?.LogWarning("skill.md YAML Front Matter 缺少结束标记 '---'，跳过解析。");
                return null;
            }
        }

        // 提取 YAML 和 Markdown 正文
        var yamlText = content[3..endMarkerIndex].Trim();
        var bodyStart = content.IndexOf('\n', endMarkerIndex + 1);
        var body = bodyStart >= 0 ? content[(bodyStart + 1)..].Trim() : string.Empty;

        // 解析 YAML
        SkillFrontMatter frontMatter;
        try
        {
            frontMatter = YamlDeserializer.Deserialize<SkillFrontMatter>(yamlText);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "skill.md YAML Front Matter 解析失败: {Error}", ex.Message);
            return null;
        }

        if (string.IsNullOrWhiteSpace(frontMatter.Name))
        {
            logger?.LogWarning("skill.md YAML Front Matter 缺少 name 字段，跳过。");
            return null;
        }

        // 构建 LLMSkillDefinition
        var definition = new LLMSkillDefinition
        {
            Name = frontMatter.Name.Trim(),
            Description = frontMatter.Description?.Trim() ?? string.Empty,
            SystemPrompt = body
        };

        // 映射 defaultTools（字符串 → LLMToolDefinition）
        if (frontMatter.DefaultTools is { Count: > 0 })
        {
            foreach (var toolName in frontMatter.DefaultTools)
            {
                if (string.IsNullOrWhiteSpace(toolName)) continue;
                definition.DefaultTools.Add(new LLMToolDefinition
                {
                    Type = "function",
                    Function = new LLMToolFunctionDefinition
                    {
                        Name = toolName.Trim(),
                        Description = $"技能 {frontMatter.Name} 默认工具: {toolName.Trim()}"
                    }
                });
            }
        }

        logger?.LogDebug("skill.md 解析成功: Name={Name}, Description={Desc}, DefaultTools={ToolCount}, SystemPromptLen={PromptLen}",
            definition.Name, definition.Description, definition.DefaultTools.Count, definition.SystemPrompt?.Length ?? 0);

        return definition;
    }

    /// <summary>
    /// YAML Front Matter 数据模型。
    /// </summary>
    private sealed class SkillFrontMatter
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> DefaultTools { get; set; }
    }
}
