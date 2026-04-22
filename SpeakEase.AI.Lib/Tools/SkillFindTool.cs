using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using System.Text.Json;


namespace SpeakEase.AI.Lib.Tools
{
    /// <summary>
    /// Skill正文内容查询工具
    /// </summary>
    /// <param name="hostEnvironment"></param>
    public sealed class SkillFindTool(IHostEnvironment hostEnvironment,ILogger<SkillFindTool> logger): IToolExecutor
    {
        /// <summary>
        /// 技能描述
        /// </summary>
        public static readonly ToolDefinition ToolDefinition = new ToolDefinition
        {
             Type = "function",
             Function = new FunctionDefinition
             {
                 Description = "查找技能的详细使用文档。当你需要使用某项技能但不确定如何操作时，调用此工具获取完整的使用说明和参数格式",
                 Name = "find_skill",
                 Parameters = new FunctionParameters
                 {
                     Type = "object",
                     Properties = new Dictionary<string, ParameterSchema>
                     {
                         ["skillName"] = new()
                         {
                             Type = "string",
                             Description = "要查找的技能名称，如 Agent Browser",
                         },
                         ["path"] = new()
                         {
                             Type = "string",
                             Description = "技能文档的文件路径，如 wwwroot/skills/agent-browser/SKILL.md",
                         }
                     },
                     Required = ["skillName"]
                 }
             }
        };

        /// <summary>
        /// 技能内容缓存
        /// </summary>
        /// <param name="SkillName">技能名称</param>
        /// <param name="Content">技能文档内容</param>
        /// <param name="Path">技能文档路径</param>
        private record class SkillContent(string SkillName, string Content, string Path);

        /// <summary>
        /// 执行工具
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
        {
            var path = string.Empty;
            var skillname = string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(arguments);
                var root = doc.RootElement;

                if(root.TryGetProperty("path", out var pathProp))
                    path = pathProp.ToString();

                if (root.TryGetProperty("skillName", out var skillNameProp))
                    skillname = skillNameProp.GetString() ?? string.Empty;

                if(string.IsNullOrEmpty(skillname))
                {
                    return new ToolResult
                    {
                        Content = "缺少必要参数：skillName（技能名称）",
                        Success = false,
                        ToolName = "find_skill"
                    };
                }

                // path 为空时根据技能名称推导默认路径
                if (string.IsNullOrEmpty(path))
                    path = $"wwwroot\\skills\\agent-browser-0.2.0\\SKILL.md";

                var skillPath = System.IO.Path.Combine(hostEnvironment.ContentRootPath, path);

                var fileinfo = new FileInfo(skillPath);

                if (!fileinfo.Exists)
                {
                    return new ToolResult
                    {
                        Content = $"未找到技能文档：{skillname} 内容",
                        Success = false,
                        ToolName = "find_skill"
                    };
                }

                using var fileStream = fileinfo.OpenRead();

                using var reader = new StreamReader(fileStream);

                string content = await reader.ReadToEndAsync();

                return new ToolResult
                {
                    Content = content,
                    Success = true,
                    ToolName = "find_skill"
                };
            }
            catch (Exception ex)
            {
                var error = $"获取Skill 内容出错:{ex.Message}";

                logger.LogError(error);

                return new ToolResult
                {
                    Content = error,
                    Success = false,
                    ToolName = "find_skill"
                };
            }
        }
    }
}
