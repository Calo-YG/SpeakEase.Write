using AINWZ.Application.LLM;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Filters;
using AINWZ.Infrastructure.LLM.Models;
using AINWZ.Infrastructure.LLM.Options;
using AINWZ.Infrastructure.LLM.Providers;
using AINWZ.Infrastructure.LLM.ToolHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// LLM 基础设施注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 LLM Provider 与服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置对象。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddLLM(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LLMOptions>()
            .Bind(configuration.GetSection(LLMOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), "LLM:BaseUrl 不能为空。")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DefaultModel), "LLM:DefaultModel 不能为空。");

        services.AddOptions<LLMLoggingOptions>()
            .Bind(configuration.GetSection("LLM:Logging"));

        services.AddHttpClient<ILLMProvider, OpenAICompatibleLLMProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LLMOptions>>().Value;
            OpenAICompatibleLLMProvider.ConfigureHttpClient(client, options);
        });

        services.AddSingleton<ILLMSkillRegistry, InMemoryLLMSkillRegistry>();
        services.AddScoped<ILLMToolHandler, EchoToolHandler>();
        services.AddScoped<ILLMToolHandler, GetCurrentTimeToolHandler>();
        services.AddScoped<ILLMToolHandler, ReadFileSummaryToolHandler>();
        services.AddScoped<ILLMToolDispatcher, LLMToolDispatcher>();
        services.AddScoped<ILLMCallLogStore, EntityFrameworkLLMCallLogStore>();
        services.AddScoped<LLMService>();
        services.AddScoped<PipelineLLMService>();
        services.AddScoped<ILLMService>(serviceProvider => serviceProvider.GetRequiredService<PipelineLLMService>());
        services.AddScoped<ILLMServiceFilter>(serviceProvider =>
        {
            return new LLMCallLoggingFilter(serviceProvider.GetRequiredService<ILLMCallLogStore>());
        });

        return services;
    }
}
