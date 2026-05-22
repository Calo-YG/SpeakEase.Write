using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Tools;
using System.Net.Http.Headers;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// AI 扩展extension
    /// </summary>
    public static class AIExtensions
    {
        /// <summary>
        /// 命名 HttpClient 标识，与 <see cref="OpenAICompatible"/> 中引用的名称对应。
        /// </summary>
        private const string HttpClientName = "SpeakEase.LLM";

        public static IServiceCollection AddChatLLM(this IServiceCollection services)
        {
            // 注册 SpeakEase.LLM 命名的 HttpClient，超时 120 秒，Accept application/json
            services.AddHttpClient(HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                
            });

            // 注册工具/技能能力实现
            services.AddTransient<IToolCapable, ToolCapable>();
            services.AddScoped<ISkilCapable, SkillCapable>();
            // OpenAI 兼容实现：同时注册为具体类和接口，调用方可注入 IChatCompatible
            services.AddScoped<OpenAICompatible>();
            services.AddScoped<IChatCompatible, OpenAICompatible>();
            // KeyedService 按工具名注册执行器，ToolCapable 通过工具名从 DI 容器获取对应执行器
            services.AddKeyedTransient<IToolExecutor, CalculateTool>(CalculateTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, CharacterNameGeneratorTool>(CharacterNameGeneratorTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, EchoTool>(EchoTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, GetCurrentTimeTool>(GetCurrentTimeTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, PowerShellTool>(PowerShellTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, RandomGeneratorTool>(RandomGeneratorTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, TextAnalyzerTool>(TextAnalyzerTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, SkillFindTool>(SkillFindTool.ToolDefinition.Function.Name);
            // 注册 ReAct Agent 服务
            services.AddScoped<IReActAgent,ReActAgent>();
            return services;
        }
    }
}
