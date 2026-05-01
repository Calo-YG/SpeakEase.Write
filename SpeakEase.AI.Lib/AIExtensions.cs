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
            services.AddHttpClient(HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                
            }).ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            }); ;

            services.AddScoped<IToolCapable, ToolCapable>();
            services.AddScoped<ISkilCapable, SkillCapable>();
            services.AddScoped<IChatCompatible, OpenAICompatible>();
            services.AddKeyedTransient<IToolExecutor, CalculateTool>(CalculateTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, CharacterNameGeneratorTool>(CharacterNameGeneratorTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, EchoTool>(EchoTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, GetCurrentTimeTool>(GetCurrentTimeTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, PowerShellTool>(PowerShellTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, RandomGeneratorTool>(RandomGeneratorTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, TextAnalyzerTool>(TextAnalyzerTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, SkillFindTool>(SkillFindTool.ToolDefinition.Function.Name);
            services.AddScoped<IReActAgent,ReActAgent>();
            return services;
        }
    }
}
