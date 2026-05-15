using SpeakEase.AI.Lib.Contract;
using SpeakEase.Write.Infrastructure.AI.Agents;
using SpeakEase.Write.Infrastructure.AI.Analysis;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Memory;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace Microsoft.Extensions.DependencyInjection;

public static class NovelAIServiceCollectionExtensions
{
    public static IServiceCollection AddNovelAI(this IServiceCollection services)
    {
        services.AddSingleton<CreationRouter>();
        services.AddSingleton<IMemoryProvider, HybridMemoryProvider>();

        services.AddScoped<CreationOrchestrator>();
        services.AddScoped<ICreationAgentContext, CreationAgentContext>();
        services.AddScoped<IContextCompressor, ContextCompressor>();
        services.AddScoped<IForeshadowAnalysisService, ForeshadowAnalysisService>();

        services.AddScoped<INovelAgent, WriteAgent>();
        services.AddScoped<INovelAgent, WorldAgent>();
        services.AddScoped<INovelAgent, OutlineAgent>();
        services.AddScoped<INovelAgent, CreationAgent>();
        services.AddScoped<INovelAgent, AuditAgent>();
        services.AddScoped<INovelAgent, CritiqueAgent>();
        services.AddScoped<INovelAgent, GeneralAgent>();

        services.AddKeyedTransient<IToolExecutor, GetCharacterTool>(GetCharacterTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetWorldSettingTool>(GetWorldSettingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetOutlineTool>(GetOutlineTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetRecentChaptersTool>(GetRecentChaptersTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetChapterTool>(GetChapterTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetForeshadowingTool>(GetForeshadowingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SearchCharactersTool>(SearchCharactersTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetRelationshipsTool>(GetRelationshipsTool.ToolDefinition.Function.Name);

        services.AddKeyedTransient<IToolExecutor, CreateCharacterTool>(CreateCharacterTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, UpdateCharacterTool>(UpdateCharacterTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateOutlineNodeTool>(CreateOutlineNodeTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetChapterBySequenceTool>(GetChapterBySequenceTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SearchOutlineTool>(SearchOutlineTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, ListVolumesTool>(ListVolumesTool.ToolDefinition.Function.Name);

        services.AddKeyedTransient<IToolExecutor, CreateChapterOutlineTool>(CreateChapterOutlineTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateForeshadowingTool>(CreateForeshadowingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, ResolveForeshadowingTool>(ResolveForeshadowingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateTimelineEventTool>(CreateTimelineEventTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetTimelineEventsTool>(GetTimelineEventsTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SaveWorldSettingTool>(SaveWorldSettingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SearchWorldSettingTool>(SearchWorldSettingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetCharacterListTool>(GetCharacterListTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetWorkInfoTool>(GetWorkInfoTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, UpdateChapterSummaryTool>(UpdateChapterSummaryTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SaveChapterContentTool>(SaveChapterContentTool.ToolDefinition.Function.Name);

        services.AddKeyedTransient<IToolExecutor, CreateRelationshipTool>(CreateRelationshipTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetCharacterGraphTool>(GetCharacterGraphTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateCharacterArcTool>(CreateCharacterArcTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetCharacterArcTool>(GetCharacterArcTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateFactionTool>(CreateFactionTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetFactionsTool>(GetFactionsTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateGeographyTool>(CreateGeographyTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetGeographyTool>(GetGeographyTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetChapterVersionsTool>(GetChapterVersionsTool.ToolDefinition.Function.Name);

        services.AddKeyedTransient<IToolExecutor, CreatePowerSystemTool>(CreatePowerSystemTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetPowerSystemTool>(GetPowerSystemTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateWorldRuleTool>(CreateWorldRuleTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetWorldRulesTool>(GetWorldRulesTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateHistoricalEventTool>(CreateHistoricalEventTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetHistoricalEventsTool>(GetHistoricalEventsTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, WebSearchTool>(WebSearchTool.ToolDefinition.Function.Name);

        services.AddHttpClient("DuckDuckGo", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "AINW-NovelCreator/1.0 (duckduckgo-search)");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
            client.BaseAddress = new Uri("https://html.duckduckgo.com");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        });

        return services;
    }
}
