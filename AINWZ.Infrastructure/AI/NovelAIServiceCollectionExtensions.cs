using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.Write.Infrastructure.AI.Agents;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
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
        services.AddSingleton<BlackboardHolder>();
        services.AddSingleton<CreationRouter>();
        services.AddSingleton<IMemoryProvider, HybridMemoryProvider>();

        services.AddScoped<WritingBlackboardBuilder>();
        services.AddScoped<CreationOrchestrator>();
        services.AddScoped<ICreationAgentContext, CreationAgentContext>();
        services.AddScoped<IForeshadowAnalysisService, ForeshadowAnalysisService>();

        services.AddScoped<INovelAgent, WriteAgent>();
        services.AddScoped<INovelAgent, WorldAgent>();
        services.AddScoped<INovelAgent, OutlineAgent>();
        services.AddScoped<INovelAgent, CreationAgent>();
        services.AddScoped<INovelAgent, AuditAgent>();

        services.AddScoped<IWriteAgent>(sp => sp.GetServices<INovelAgent>().OfType<IWriteAgent>().First());
        services.AddScoped<IWorldAgent>(sp => sp.GetServices<INovelAgent>().OfType<IWorldAgent>().First());
        services.AddScoped<IOutlineAgent>(sp => sp.GetServices<INovelAgent>().OfType<IOutlineAgent>().First());
        services.AddScoped<ICreationAgent>(sp => sp.GetServices<INovelAgent>().OfType<ICreationAgent>().First());
        services.AddScoped<IAuditAgent>(sp => sp.GetServices<INovelAgent>().OfType<IAuditAgent>().First());

        services.AddKeyedTransient<IToolExecutor, GetCharacterTool>(GetCharacterTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetWorldSettingTool>(GetWorldSettingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetOutlineTool>(GetOutlineTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetRecentChaptersTool>(GetRecentChaptersTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SearchCharactersTool>(SearchCharactersTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetChapterTool>(GetChapterTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetForeshadowingTool>(GetForeshadowingTool.ToolDefinition.Function.Name);
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

        return services;
    }
}
