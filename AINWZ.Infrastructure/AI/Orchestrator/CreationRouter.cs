using Microsoft.Extensions.Logging;

using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

/// <summary>
/// 兼容路由入口。意图理解委托 IntentResolver，Plan 由下游编译和校验。
/// </summary>
public sealed class CreationRouter(
    ILogger<CreationRouter> logger,
    IntentResolver intentResolver = null)
{
    private readonly IntentResolver _intentResolver = intentResolver ?? new IntentResolver();

    public async Task<RouteResult> DecideWithLLMAsync(
        string userMessage,
        IEnumerable<INovelAgent> agents,
        IOpenAIContext llmContext,
        IChatCompatible llm,
        CancellationToken cancellationToken = default)
    {
        var agentList = agents.ToList();
        try
        {
            var intent = await _intentResolver.ResolveAsync(
                userMessage,
                agentList,
                llmContext,
                llm,
                cancellationToken);
            var selected = agentList.FirstOrDefault(x =>
                x.Name.Equals(intent.PrimaryAgent, StringComparison.OrdinalIgnoreCase));

            return new RouteResult
            {
                AgentName = selected?.Name ?? "general",
                ContentType = selected?.Metadata.ContentType ?? "plain",
                Reason = intent.Reason,
                Confidence = intent.Confidence,
                Goals = intent.Goals.ToList(),
                NeedsClarification = intent.NeedsClarification,
                ClarificationQuestion = intent.ClarificationQuestion,
                Pipeline = intent.ExplicitSequence.ToList()
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Intent resolution failed; falling back to general Agent.");
            var fallback = agentList.FirstOrDefault(x => x.Name == "general") ?? agentList.FirstOrDefault();
            return new RouteResult
            {
                AgentName = fallback?.Name ?? "general",
                ContentType = fallback?.Metadata.ContentType ?? "plain",
                Reason = "Intent resolution failed.",
                Confidence = 0
            };
        }
    }
}

public sealed class RouteResult
{
    public string AgentName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> Goals { get; set; } = new();
    public bool NeedsClarification { get; set; }
    public string ClarificationQuestion { get; set; } = string.Empty;
    public List<string> Pipeline { get; set; } = new();
}
