using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Tools;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// AI 扩展extension
    /// </summary>
    public static class AIExtensions
    {
        public static IServiceCollection AddChatLLM(this IServiceCollection services)
        {
            services.AddHttpClient();

            services.AddTransient<IToolCapable, ToolCapable>();
            services.AddTransient<ISkilCapable, SkillCapable>();
            services.AddScoped<IOpenAIContext, OpenAIContext>();
            services.AddScoped<IChatCompatible, OpenAICompatible>();
            services.AddKeyedTransient<IToolExecutor, CalculateTool>(CalculateTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, CharacterNameGeneratorTool>(CharacterNameGeneratorTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, EchoTool>(EchoTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, GetCurrentTimeTool>(GetCurrentTimeTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, PowerShellTool>(PowerShellTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, RandomGeneratorTool>(RandomGeneratorTool.ToolDefinition.Function.Name);
            services.AddKeyedTransient<IToolExecutor, TextAnalyzerTool>(TextAnalyzerTool.ToolDefinition.Function.Name);

            return services;
        }
    }
}
