using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class AuditAgent : IAuditAgent
{
    private readonly IChatCompatible _llm;
    private readonly IToolCapable _tools;

    public AuditAgent(IChatCompatible llm, IToolCapable tools)
    {
        _llm = llm;
        _tools = tools;
    }

    public string Name => "audit";

    public string DisplayName => "审核Agent";

    public string AuditScope => "全作品一致性审查";

    public string BuildPrompt()
    {
        return """
# 角色
你是严格的审稿编辑，擅长发现故事中的逻辑漏洞和一致性问题。

# 你的检查清单
1. □ 人物性格是否前后一致？→ 调用 get_character 核实
2. □ 世界观规则是否被违反？→ 调用 get_world_setting 核实
3. □ 伏笔是否有回收？→ 调用 get_foreshadowing 核实
4. □ 时间线是否有矛盾？→ 调用 get_outline 比对
5. □ 章节之间的衔接是否流畅？→ 调用 get_recent_chapters 对比
6. □ 叙事视角是否统一？→ 检查全文
7. □ 节奏是否有问题？→ 结合大纲和卷结构判断

# 信息获取方式
你拥有一组查询工具，可在审核过程中按需调用：
- 获取待审章节内容 → 调用 get_chapter 或 get_chapter_by_sequence
- 核实角色设定 → 调用 get_character 或 search_characters
- 核实世界规则 → 调用 get_world_setting
- 比对大纲走向 → 调用 get_outline 或 search_outline
- 查伏笔回收状态 → 调用 get_foreshadowing
- 回顾前文衔接 → 调用 get_recent_chapters
- 查看卷/章结构 → 调用 list_volumes
- 检查角色关系网 → 调用 get_relationships
- 快速浏览所有角色 → 调用 get_character_list
- 记录发现的伏笔/问题 → 调用 create_foreshadowing

# 决策原则
1. 先查后判 — 发现疑似问题时，先调用工具确认再下结论
2. 按需查询 — 只查询与当前检查点相关的信息
3. 证据充分 — 每个问题必须引用具体文本作为证据

# 输出要求
- 先给出总体评价（通过/需修改/大改）
- 列出每个问题的严重程度（高/中/低）
- 给出具体修改建议，引用原文
- 如无问题，明确说"通过"
""";
    }

    public void RegisterTools(IToolCapable toolCapable)
    {
        toolCapable.RegisterTool(GetWorkInfoTool.ToolDefinition);
        toolCapable.RegisterTool(GetChapterTool.ToolDefinition);
        toolCapable.RegisterTool(GetChapterBySequenceTool.ToolDefinition);
        toolCapable.RegisterTool(GetCharacterTool.ToolDefinition);
        toolCapable.RegisterTool(SearchCharactersTool.ToolDefinition);
        toolCapable.RegisterTool(GetWorldSettingTool.ToolDefinition);
        toolCapable.RegisterTool(GetOutlineTool.ToolDefinition);
        toolCapable.RegisterTool(SearchOutlineTool.ToolDefinition);
        toolCapable.RegisterTool(GetForeshadowingTool.ToolDefinition);
        toolCapable.RegisterTool(GetRecentChaptersTool.ToolDefinition);
        toolCapable.RegisterTool(ListVolumesTool.ToolDefinition);
        toolCapable.RegisterTool(GetRelationshipsTool.ToolDefinition);
        toolCapable.RegisterTool(CreateForeshadowingTool.ToolDefinition);
        toolCapable.RegisterTool(SearchWorldSettingTool.ToolDefinition);
        toolCapable.RegisterTool(GetCharacterListTool.ToolDefinition);
    }

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RegisterTools(_tools);

        var messages = BuildMessages(request);
        var ctx = new LLMTurnContext { Model = request.Model, Temperature = request.Temperature, MaxTokens = request.MaxTokens };

        for (int i = 0; i < request.MaxIterations; i++)
        {
            LLMTurnResult turnResult = null;

            await foreach (var tc in _llm.StreamAsync(ctx, messages, _tools.Tools, cancellationToken))
            {
                switch (tc.Type)
                {
                    case "content":
                        yield return new AgentStreamChunk { Type = "content", Content = tc.Content };
                        break;
                    case "tool_call":
                        yield return new AgentStreamChunk { Type = "tool_call", ToolCallDelta = tc.ToolCallDelta };
                        break;
                    case "done":
                        turnResult = tc.TurnResult;
                        break;
                }
            }

            if (turnResult == null) continue;

            if (turnResult.HasToolCalls)
            {
                messages.Add(new AssistantMessage { Content = turnResult.Content ?? string.Empty, ToolCalls = turnResult.ToolCalls });
                foreach (var tc in turnResult.ToolCalls)
                {
                    var tr = await _tools.ExecuteAsync(tc, cancellationToken);
                    yield return new AgentStreamChunk { Type = "tool_result", ToolResult = tr };
                    messages.Add(ChatMessage.Tool(tc.Id, tr.Content ?? string.Empty));
                }
            }
            else
            {
                messages.Add(ChatMessage.Assistant(turnResult.Content));
                yield return new AgentStreamChunk { Type = "done", FinalResponse = new AgentResponse { Content = turnResult.Content, Model = turnResult.Model, Iterations = i + 1, StopReason = "completed" } };
                yield break;
            }
        }
    }

    private static List<ChatMessage> BuildMessages(AgentRequest req)
    {
        var msgs = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(req.SystemPrompt)) msgs.Add(ChatMessage.System(req.SystemPrompt));
        if (req.ConversationHistory?.Count > 0) msgs.AddRange(req.ConversationHistory);
        if (!string.IsNullOrEmpty(req.UserMessage)) msgs.Add(ChatMessage.User(req.UserMessage));
        return msgs;
    }
}
