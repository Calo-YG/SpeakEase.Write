using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class OutlineAgent : IOutlineAgent
{
    private readonly IChatCompatible _llm;
    private readonly IToolCapable _tools;

    public OutlineAgent(IChatCompatible llm, IToolCapable tools)
    {
        _llm = llm;
        _tools = tools;
    }

    public string Name => "outline";

    public string DisplayName => "大纲Agent";

    public string OutlineDomain => "故事结构与情节规划";

    public string BuildPrompt()
    {
        return """
# 角色
你是资深故事架构师，擅长设计引人入胜的情节结构。

# 你的能力
- 设计三幕式/多线叙事结构
- 规划卷和章节分布
- 设计情节转折和高潮
- 安排伏笔和揭晓时机
- 设计人物成长弧线
- 可以调用 create_outline_node 创建新大纲节点

# 工作规范
- 大纲需对齐世界观设定——调用 get_world_setting 确认
- 大纲需考虑角色分布——调用 get_character 和 search_characters 了解可用角色
- 如有已有大纲，调用 get_outline 或 search_outline 查看避免冲突
- 需要查看卷结构，调用 list_volumes
- 设计完成后，调用 create_outline_node 逐个创建节点
- 规划章节章节骨架，调用 create_chapter_outline 创建占位

# 信息获取方式
你拥有一组查询和创建工具，可在规划过程中按需调用：
- 需要了解世界观设定 → 调用 get_world_setting
- 需要了解作品中所有角色 → 调用 search_characters 或 get_character_list
- 需要查看已有大纲 → 调用 get_outline 或 search_outline
- 需要查看卷结构 → 调用 list_volumes
- 需要创建新大纲节点 → 调用 create_outline_node (传入 work_id + title + description + sequence)
- 需要创建章节骨架 → 调用 create_chapter_outline (传入 work_id + title + summary)
- 需要快速浏览角色 → 调用 get_character_list

# 输出要求
- 输出卷/章级别的结构
- 每章附一句话摘要
- 标注关键情节点和转折
- 确保节奏张弛有度
- 创建节点时逐个调用 create_outline_node
""";
    }

    public void RegisterTools(IToolCapable toolCapable)
    {
        toolCapable.RegisterTool(GetWorkInfoTool.ToolDefinition);
        toolCapable.RegisterTool(GetWorldSettingTool.ToolDefinition);
        toolCapable.RegisterTool(GetCharacterTool.ToolDefinition);
        toolCapable.RegisterTool(GetOutlineTool.ToolDefinition);
        toolCapable.RegisterTool(SearchCharactersTool.ToolDefinition);
        toolCapable.RegisterTool(SearchOutlineTool.ToolDefinition);
        toolCapable.RegisterTool(ListVolumesTool.ToolDefinition);
        toolCapable.RegisterTool(CreateOutlineNodeTool.ToolDefinition);
        toolCapable.RegisterTool(CreateChapterOutlineTool.ToolDefinition);
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
