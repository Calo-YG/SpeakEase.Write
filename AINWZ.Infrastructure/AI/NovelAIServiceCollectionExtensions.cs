using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.Write.Infrastructure.AI;
using SpeakEase.Write.Infrastructure.AI.Agents;
using SpeakEase.Write.Infrastructure.AI.Analysis;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Memory;
using SpeakEase.Write.Infrastructure.AI.Runtime;
using SpeakEase.Write.Infrastructure.AI.Character;
using ApplicationMemoryProvider = SpeakEase.Write.Application.Abstractions.AI.IMemoryProvider;
using ApplicationAgentOrchestrator = SpeakEase.Write.Application.Abstractions.AI.IAgentOrchestrator;
using ApplicationCreationRuntimeFacade = SpeakEase.Write.Application.Abstractions.AI.ICreationRuntimeFacade;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Infrastructure.AI.Tools;
using ApplicationAgentRunStore = SpeakEase.Write.Application.Abstractions.AI.IAgentRunStore;
using ApplicationMemoryRefreshQueue = SpeakEase.Write.Application.Abstractions.AI.IMemoryRefreshQueue;
using ApplicationMemoryRefreshFailureHandler = SpeakEase.Write.Application.Abstractions.AI.IMemoryRefreshFailureHandler;
using RuntimeToolRegistry = SpeakEase.AI.Lib.Runtime.IToolRegistry;
using RuntimeToolExposurePolicy = SpeakEase.AI.Lib.Runtime.ToolExposurePolicy;
using RuntimeLegacyToolRegistryAdapter = SpeakEase.AI.Lib.Runtime.LegacyToolRegistryAdapter;
using RuntimePromptProfileCatalog = SpeakEase.AI.Lib.Runtime.PromptProfileCatalog;
using RuntimePolicyProfileCatalog = SpeakEase.AI.Lib.Runtime.PolicyProfileCatalog;
using RuntimePromptCompiler = SpeakEase.AI.Lib.Runtime.PromptCompiler;
using RuntimeAgentLoop = SpeakEase.AI.Lib.Runtime.IAgentLoop;
using RuntimeAgentLoopImplementation = SpeakEase.AI.Lib.Runtime.AgentLoop;
using RuntimeHost = SpeakEase.AI.Lib.Runtime.RuntimeHost;
using RuntimeRunner = SpeakEase.AI.Lib.Runtime.IAgentRuntimeRunner;
using RuntimeRunnerImplementation = SpeakEase.AI.Lib.Runtime.AgentRuntimeRunner;
using RuntimeStepScheduler = SpeakEase.AI.Lib.Runtime.IStepScheduler;
using RuntimeLinearStepScheduler = SpeakEase.AI.Lib.Runtime.LinearStepScheduler;
using RuntimeStateStore = SpeakEase.AI.Lib.Runtime.IRuntimeStateStore;
using ApplicationAgentRuntimeStore = SpeakEase.Write.Application.Abstractions.AI.IAgentRuntimeStore;
using RuntimeEventSink = SpeakEase.AI.Lib.Runtime.IRuntimeEventSink;
using RuntimeEventSinkImplementation = SpeakEase.Write.Infrastructure.AI.Runtime.AgentRuntimeEventSink;
using ApplicationCharacterStateStore = SpeakEase.Write.Application.Abstractions.Story.ICharacterStateStore;
using ApplicationCharacterStateEvaluator = SpeakEase.Write.Application.Abstractions.Story.ICharacterStateEvaluator;
using ApplicationGrowthConsistencyValidator = SpeakEase.Write.Application.Abstractions.Story.IGrowthConsistencyValidator;
using ApplicationPlotHookGenerator = SpeakEase.Write.Application.Abstractions.Story.IPlotHookGenerator;
using ApplicationCharacterRuntimeQueue = SpeakEase.Write.Application.Abstractions.Story.ICharacterRuntimeQueue;
using ApplicationCharacterRuntimeProcessor = SpeakEase.Write.Application.Abstractions.Story.ICharacterRuntimeProcessor;
using ApplicationCharacterStateProposalExtractor = SpeakEase.Write.Application.Abstractions.Story.ICharacterStateProposalExtractor;
using ApplicationMemoryContextProvider = SpeakEase.Write.Application.Abstractions.Memory.IMemoryContextProvider;

namespace Microsoft.Extensions.DependencyInjection;

// 小说 AI 模块 DI 注册扩展：注册所有 Agent、工具、上下文构建器、记忆提供者
public static class NovelAIServiceCollectionExtensions
{
    public static IServiceCollection AddNovelAI(this IServiceCollection services)
    {
        // 核心编排组件
        services.AddSingleton<IntentResolver>();
        services.AddSingleton<CreationRouter>();
        services.AddSingleton<PlanResolver>();
        services.AddSingleton<PlanCompiler>();
        services.AddScoped<ApplicationAgentRunStore, AgentRunStore>();
        services.AddScoped<ApplicationAgentRuntimeStore, AgentRuntimeStore>();
        services.AddScoped<RuntimeStateStore>(sp =>
            (RuntimeStateStore)sp.GetRequiredService<ApplicationAgentRuntimeStore>());
        services.AddScoped<RuntimeEventSink, RuntimeEventSinkImplementation>();
        services.AddScoped<IMemoryProvider, HybridMemoryProvider>();
        services.AddScoped<ApplicationMemoryProvider>(sp => sp.GetRequiredService<IMemoryProvider>());
        services.AddScoped<ApplicationMemoryRefreshFailureHandler>(sp =>
            (ApplicationMemoryRefreshFailureHandler)sp.GetRequiredService<IMemoryProvider>());
        services.AddScoped<ApplicationMemoryContextProvider, MemoryContextProvider>();
        services.AddSingleton<MemoryRefreshQueue>();
        services.AddSingleton<ApplicationMemoryRefreshQueue>(sp => sp.GetRequiredService<MemoryRefreshQueue>());
        services.AddHostedService(sp => sp.GetRequiredService<MemoryRefreshQueue>());
        services.AddScoped<IToolExecutionGuard, WorkToolExecutionGuard>();
        services.AddScoped<IChatCompatible, LoggingChatCompatible>();
        services.AddScoped<RuntimeToolRegistry, RuntimeLegacyToolRegistryAdapter>();
        services.AddScoped<RuntimeToolExposurePolicy>();
        services.AddSingleton<RuntimePromptProfileCatalog>();
        services.AddSingleton<RuntimePolicyProfileCatalog>();
        services.AddSingleton<RuntimePromptCompiler>();
        services.AddScoped<RuntimeAgentLoop, RuntimeAgentLoopImplementation>();
        services.AddScoped<RuntimeHost>();
        services.AddSingleton<RuntimeStepScheduler, RuntimeLinearStepScheduler>();
        services.AddScoped<RuntimeRunner, RuntimeRunnerImplementation>();
        services.AddOptions<AgentRuntimeModeOptions>().BindConfiguration(AgentRuntimeModeOptions.SectionName);
        services.AddScoped<ApplicationCharacterStateStore, CharacterStateStore>();
        services.AddSingleton<ApplicationCharacterStateEvaluator, CharacterStateEvaluator>();
        services.AddSingleton<ApplicationGrowthConsistencyValidator, GrowthConsistencyValidator>();
        services.AddSingleton<ApplicationPlotHookGenerator, PlotHookGenerator>();
        services.AddScoped<ApplicationCharacterRuntimeProcessor, CharacterRuntimeProcessor>();
        services.AddScoped<ApplicationCharacterStateProposalExtractor, CharacterStateProposalExtractor>();
        services.AddSingleton<CharacterRuntimeWorker>();
        services.AddSingleton<ApplicationCharacterRuntimeQueue>(sp => sp.GetRequiredService<CharacterRuntimeWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<CharacterRuntimeWorker>());

        // 上下文管理与伏笔分析
        services.AddScoped<CreationOrchestrator>();
        services.AddScoped<CreationRuntimeFacade>();
        services.AddScoped<ArtifactContextBuilder>();
        services.AddScoped<ApplicationCreationRuntimeFacade>(sp => sp.GetRequiredService<CreationRuntimeFacade>());
        services.AddScoped<ApplicationAgentOrchestrator>(sp => sp.GetRequiredService<CreationRuntimeFacade>());
        services.AddScoped<ICreationAgentContext, CreationAgentContext>();
        services.AddSingleton<LayeredContextAssembler>();
        services.AddScoped<IContextCompressor, ContextCompressor>();
        services.AddScoped<IForeshadowAnalysisService, ForeshadowAnalysisService>();

        // 7 个创作 Agent：write / world / outline / creation / audit / critique / general
        services.AddScoped<INovelAgent, WriteAgent>();
        services.AddScoped<INovelAgent, WorldAgent>();
        services.AddScoped<INovelAgent, OutlineAgent>();
        services.AddScoped<INovelAgent, CreationAgent>();
        services.AddScoped<INovelAgent, AuditAgent>();
        services.AddScoped<INovelAgent, CritiqueAgent>();
        services.AddScoped<INovelAgent, GeneralAgent>();

        // 查询类工具（按 Keyed DI 注册，键名为工具函数名）
        services.AddKeyedTransient<IToolExecutor, GetWorldSettingTool>(GetWorldSettingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetOutlineTool>(GetOutlineTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetRecentChaptersTool>(GetRecentChaptersTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetChapterTool>(GetChapterTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetForeshadowingTool>(GetForeshadowingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SearchCharactersTool>(SearchCharactersTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetRelationshipsTool>(GetRelationshipsTool.ToolDefinition.Function.Name);

        // 写入类工具
        services.AddKeyedTransient<IToolExecutor, UpdateCharacterTool>(UpdateCharacterTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateOutlineNodeTool>(CreateOutlineNodeTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateOutlineTool>(CreateOutlineTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetChapterBySequenceTool>(GetChapterBySequenceTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SearchOutlineTool>(SearchOutlineTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, ListVolumesTool>(ListVolumesTool.ToolDefinition.Function.Name);

        services.AddKeyedTransient<IToolExecutor, CreateCharacterTool>(CreateCharacterTool.ToolDefinition.Function.Name);
        // 创建/更新类工具
        services.AddKeyedTransient<IToolExecutor, CreateChapterOutlineTool>(CreateChapterOutlineTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, ResolveForeshadowingTool>(ResolveForeshadowingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateTimelineEventTool>(CreateTimelineEventTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetTimelineEventsTool>(GetTimelineEventsTool.ToolDefinition.Function.Name);
        // 伏笔/时间线工具
        services.AddKeyedTransient<IToolExecutor, CreateForeshadowingTool>(CreateForeshadowingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SearchWorldSettingTool>(SearchWorldSettingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetCharacterListTool>(GetCharacterListTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetWorkInfoTool>(GetWorkInfoTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, UpdateChapterSummaryTool>(UpdateChapterSummaryTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SaveChapterContentTool>(SaveChapterContentTool.ToolDefinition.Function.Name);

        // 世界观/章节工具
        services.AddKeyedTransient<IToolExecutor, SaveWorldSettingTool>(SaveWorldSettingTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetCharacterGraphTool>(GetCharacterGraphTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateCharacterArcTool>(CreateCharacterArcTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetCharacterArcTool>(GetCharacterArcTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateFactionTool>(CreateFactionTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetFactionsTool>(GetFactionsTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateGeographyTool>(CreateGeographyTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetGeographyTool>(GetGeographyTool.ToolDefinition.Function.Name);
        // 角色关系/图谱/成长弧工具
        services.AddKeyedTransient<IToolExecutor, CreateRelationshipTool>(CreateRelationshipTool.ToolDefinition.Function.Name);

        // 版本历史工具
        services.AddKeyedTransient<IToolExecutor, GetChapterVersionsTool>(GetChapterVersionsTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetPowerSystemTool>(GetPowerSystemTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateWorldRuleTool>(CreateWorldRuleTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetWorldRulesTool>(GetWorldRulesTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateHistoricalEventTool>(CreateHistoricalEventTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetHistoricalEventsTool>(GetHistoricalEventsTool.ToolDefinition.Function.Name);
        // 世界力量体系/规则/历史事件工具
        services.AddKeyedTransient<IToolExecutor, CreatePowerSystemTool>(CreatePowerSystemTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, SaveWritingRulesTool>(SaveWritingRulesTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, GetWritingRulesTool>(GetWritingRulesTool.ToolDefinition.Function.Name);
        // 网络搜索 + 写作规则工具
        services.AddKeyedTransient<IToolExecutor, WebSearchTool>(WebSearchTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateCharacterGraphNodeTool>(CreateCharacterGraphNodeTool.ToolDefinition.Function.Name);
        services.AddKeyedTransient<IToolExecutor, CreateCharacterGraphEdgeTool>(CreateCharacterGraphEdgeTool.ToolDefinition.Function.Name);

        // 角色关系图谱工具
        services.AddKeyedTransient<IToolExecutor, CreateCharacterGraphTool>(CreateCharacterGraphTool.ToolDefinition.Function.Name);

        // DuckDuckGo 搜索 HTTP 客户端注册
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
