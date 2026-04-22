using Microsoft.Extensions.Hosting;
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
    public sealed class SkillFindTool(IHostEnvironment hostEnvironment): IToolExecutor
    {
        /// <summary>
        /// 技能描述
        /// </summary>
        public static ToolDefinition ToolDefinition = new ToolDefinition
        {
             Type = "function",
             Function = new FunctionDefinition
             {
                 Description = "如果你需要用到某项技能，可以帮你查询到技能的具体描述包括工具调用等等",
                 Name = "findskill",
                 Parameters = new FunctionParameters
                 {
                     Type = "object",
                     Properties = new Dictionary<string, ParameterSchema>
                     {
                         ["path"] = new()
                         {
                             Type = "string",
                             Description = "技能路径方便查找技能具体内容",
                         },
                         ["skillName"] = new()
                         {
                             Type = "string",
                             Description = "技能名称",
                         }
                     }
                 }
             }
        };

        /// <summary>
        /// tool 内部工具定义
        /// </summary>
        /// <param name="SkillName"></param>
        /// <param name="Content"></param>
        /// <param name="Path"></param>
        private record class TookSikillDedintion(string SkillName, string Content, string Path);

        /// <summary>
        /// 技能定义
        /// </summary>
        private readonly Dictionary<string, TookSikillDedintion> SkillDic = new Dictionary<string, TookSikillDedintion>();

        /// <summary>
        /// 执行工具
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
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
                    skillname = skillname.ToString();

                if(string.IsNullOrEmpty(path) || string.IsNullOrEmpty(skillname))
                {
                    //return new ToolResult
                    //{

                    //}
                }

                var skillPath = Path.Combine(hostEnvironment.ContentRootPath, path);

                 var fileinfo = new FileInfo(path);

                if (!fileinfo.Exists)
                {

                }

                //var file = 

            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}
