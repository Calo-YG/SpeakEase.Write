using System.Runtime.CompilerServices;
using System.Text;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.AI.Lib.Tools;

namespace SpeakEase.AI.Lib;

/// <summary>
/// 历史 ReActAgent 兼容入口。
/// 实际执行委托给 AgentLoop，保留原有 Tool、Skill、同步和流式 API。
/// </summary>
public sealed class ReActAgent(
    IToolCapable toolCapable,
    ISkilCapable skilCapable,
    IChatCompatible llmStrategy) : IReActAgent
{
    private const string DefaultSystemPrompt = @"# 角色
你是 AI 智能助手，具备工具调用能力。通过 Function Calling 调用工具获取外部信息，也可基于自身知识直接回答。

# 决策流程
面对每个请求，先判断是否需要外部信息或操作；需要时调用对应工具，不需要时直接回答。工具返回后检查信息是否足够，信息不足时继续调用，不要重复相同失败调用。

# 输出规范
直接用自然语言回答用户，无需输出 Thought/Action/Observation 等格式。回答要准确、完整、有条理。

# 约束
1. 先判断再行动，不要为了调用工具而调用工具
2. 工具失败时分析原因并换路
3. 信息充足后立即回答
4. 达到迭代上限后基于当前信息给出最佳进展";

    private readonly AgentLoop _agentLoop = new();
    private readonly ISkillResolver _skillResolver = new LegacySkillResolverAdapter(skilCapable);
    private bool _initialized;

    public void Init()
    {
        if (_initialized)
            return;

        _initialized = true;
        toolCapable.RegisterTool(EchoTool.ToolDefinition);
        toolCapable.RegisterTool(CharacterNameGeneratorTool.ToolDefinition);
        toolCapable.RegisterTool(CalculateTool.ToolDefinition);
        toolCapable.RegisterTool(GetCurrentTimeTool.ToolDefinition);
        toolCapable.RegisterTool(PowerShellTool.ToolDefinition);
        toolCapable.RegisterTool(RandomGeneratorTool.ToolDefinition);
        toolCapable.RegisterTool(TextAnalyzerTool.ToolDefinition);
        toolCapable.RegisterTool(SkillFindTool.ToolDefinition);
        skilCapable.RegiSkill(new SkillDefinition
        {
            Description = "无头浏览器自动化，支持网页导航、点击、输入、截图，内置 PowerShell 执行和网络搜索能力",
            Name = "Agent Browser",
            Path = @"wwwroot\skills\agent-browser-0.2.0\SKILL.md"
        });
    }

    public async Task<AgentResponse> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AgentResponse finalResponse = null;

        await foreach (var chunk in ExecuteStreamAsync(request, cancellationToken))
        {
            if (chunk.Type == "done" && chunk.FinalResponse is not null)
                finalResponse = chunk.FinalResponse;
        }

        return finalResponse ?? new AgentResponse
        {
            Content = string.Empty,
            Model = request.Model,
            StopReason = "cancelled"
        };
    }

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Init();

        var loopRequest = new AgentRequest
        {
            RunId = request.RunId,
            StepId = request.StepId,
            Model = request.Model,
            SystemPrompt = BuildSystemPrompt(request),
            UserMessage = request.UserMessage,
            ConversationHistory = request.ConversationHistory is null
                ? new List<ChatMessage>()
                : new List<ChatMessage>(request.ConversationHistory),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            ContextWindowTokens = request.ContextWindowTokens,
            TopP = request.TopP,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty,
            MaxIterations = request.MaxIterations,
            SkillName = request.SkillName,
            WorkId = request.WorkId,
            UserId = request.UserId,
            EnableAutoToolDispatch = request.EnableAutoToolDispatch,
            Journal = request.Journal
        };

        await foreach (var chunk in _agentLoop.RunAsync(new AgentLoopRequest
        {
            AgentName = nameof(ReActAgent),
            Llm = llmStrategy,
            Tools = toolCapable,
            SkillResolver = _skillResolver,
            Journal = loopRequest.Journal,
            Request = loopRequest
        }, cancellationToken))
        {
            yield return chunk;
        }
    }

    private string BuildSystemPrompt(AgentRequest request)
    {
        var builder = new StringBuilder(
            string.IsNullOrWhiteSpace(request.SystemPrompt)
                ? DefaultSystemPrompt
                : request.SystemPrompt);
        var skillPrompt = skilCapable.BuildSkillPropmt();

        if (!string.IsNullOrWhiteSpace(skillPrompt))
        {
            if (builder.Length > 0)
                builder.AppendLine();
            builder.Append(skillPrompt);
        }

        return builder.ToString();
    }
}
