using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class WriteAgent : IWriteAgent
{
    private readonly IChatCompatible _llm;
    private readonly IToolCapable _tools;

    public WriteAgent(IChatCompatible llm, IToolCapable tools)
    {
        _llm = llm;
        _tools = tools;
    }

    public string Name => "write";

    public string DisplayName => "写作Agent";

    public string WritingStyle => "文学性创作";

    public string BuildPrompt()
    {
        return """
# 角色
你是资深小说写手，擅长各种风格的文字创作。

# 你的能力
- 续写章节
- 润色文字
- 扩写段落
- 重写不满意片段

# 写作规范
- 遵循已建立的世界观设定——如不确定细节，调用 get_world_setting 查询
- 遵循已有大纲路径——如不确定后续走向，调用 get_outline 查看
- 保持人物性格一致性——写涉及某角色时，先调用 get_character 确认其性格和说话风格
- 注意伏笔和前后呼应——参考前文时调用 get_recent_chapters

# 信息获取方式
你拥有一组查询工具，可在写作过程中按需调用：
- 需要了解作品基本信息（简介/题材/风格/字数）→ 调用 get_work_info
- 需要世界观规则、地理、势力信息 → 调用 get_world_setting 或 search_world_setting
- 需要大纲结构、章节规划 → 调用 search_outline 或 get_outline
- 需要了解某个角色的性格、背景、说话风格 → 调用 get_character
- 需要模糊搜索某类角色 → 调用 search_characters
- 需要回顾前文内容 → 调用 get_recent_chapters
- 需要查看特定章节 → 调用 get_chapter 或 get_chapter_by_sequence
- 需要查看卷结构 → 调用 list_volumes
- 需要了解角色关系 → 调用 get_relationships
- 需要快速浏览所有角色 → 调用 get_character_list
- 需要创建章节骨架 → 调用 create_chapter_outline
- 需要记录新伏笔 → 调用 create_foreshadowing

# 决策原则
1. 先查后写 — 涉及具体设定、角色时，先调用工具确认再动笔
2. 按需查询 — 不需要的信息不要主动查询，节省上下文空间
3. 一次查准 — 尽量精确传参，避免多次查询同类信息

# 输出要求
- 直接输出完整的章节内容，无需输出思考过程
- 每段尽量不超过 300 字
- 注意段落间的过渡自然
""";
    }

    public void RegisterTools(IToolCapable toolCapable)
    {
        toolCapable.RegisterTool(GetWorkInfoTool.ToolDefinition);
        toolCapable.RegisterTool(GetWorldSettingTool.ToolDefinition);
        toolCapable.RegisterTool(GetOutlineTool.ToolDefinition);
        toolCapable.RegisterTool(GetCharacterTool.ToolDefinition);
        toolCapable.RegisterTool(SearchCharactersTool.ToolDefinition);
        toolCapable.RegisterTool(GetRecentChaptersTool.ToolDefinition);
        toolCapable.RegisterTool(GetChapterTool.ToolDefinition);
        toolCapable.RegisterTool(GetChapterBySequenceTool.ToolDefinition);
        toolCapable.RegisterTool(SearchOutlineTool.ToolDefinition);
        toolCapable.RegisterTool(ListVolumesTool.ToolDefinition);
        toolCapable.RegisterTool(GetRelationshipsTool.ToolDefinition);
        toolCapable.RegisterTool(CreateChapterOutlineTool.ToolDefinition);
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
