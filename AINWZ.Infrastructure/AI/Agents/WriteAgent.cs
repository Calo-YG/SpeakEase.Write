using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class WriteAgent : IWriteAgent
{
    private readonly IReActAgent _react;
    private readonly IOpenAIContext _llmContext;
    private readonly IToolCapable _toolCapable;
    private bool _toolsInitialized;

    public WriteAgent(
        IReActAgent react,
        IOpenAIContext llmContext,
        IToolCapable toolCapable)
    {
        _react = react;
        _llmContext = llmContext;
        _toolCapable = toolCapable;
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
- 需要世界观规则、地理、势力信息 → 调用 get_world_setting
- 需要大纲结构、章节规划 → 调用 get_outline
- 需要了解某个角色的性格、背景、说话风格 → 调用 get_character
- 需要模糊搜索某类角色 → 调用 search_characters
- 需要回顾前文内容 → 调用 get_recent_chapters
- 需要查看特定章节 → 调用 get_chapter

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
        if (_toolsInitialized) return;
        _toolsInitialized = true;

        toolCapable.RegisterTool(GetWorldSettingTool.ToolDefinition);
        toolCapable.RegisterTool(GetOutlineTool.ToolDefinition);
        toolCapable.RegisterTool(GetCharacterTool.ToolDefinition);
        toolCapable.RegisterTool(SearchCharactersTool.ToolDefinition);
        toolCapable.RegisterTool(GetRecentChaptersTool.ToolDefinition);
        toolCapable.RegisterTool(GetChapterTool.ToolDefinition);
    }

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RegisterTools(_toolCapable);

        await _llmContext.ResolveAsync(cancellationToken);
        request.Model = _llmContext.Model;

        await foreach (var chunk in _react.ExecuteStreamAsync(request, cancellationToken))
        {
            yield return chunk;
        }
    }
}
