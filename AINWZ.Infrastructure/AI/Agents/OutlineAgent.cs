using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class OutlineAgent : IOutlineAgent
{
    private readonly IReActAgent _react;
    private readonly IOpenAIContext _llmContext;
    private readonly IToolCapable _toolCapable;
    private bool _toolsInitialized;

    public OutlineAgent(
        IReActAgent react,
        IOpenAIContext llmContext,
        IToolCapable toolCapable)
    {
        _react = react;
        _llmContext = llmContext;
        _toolCapable = toolCapable;
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

# 工作规范
- 大纲需对齐世界观设定——调用 get_world_setting 确认
- 大纲需考虑角色分布——调用 get_characters 了解可用角色
- 如有已有大纲，调用 get_existing_outline 查看避免冲突

# 信息获取方式
你拥有一组查询工具，可在规划过程中按需调用：
- 需要了解世界观设定 → 调用 get_world_setting
- 需要了解作品中所有角色 → 调用 get_characters
- 需要查看已有大纲 → 调用 get_existing_outline

# 输出要求
- 输出卷/章级别的结构
- 每章附一句话摘要
- 标注关键情节点和转折
- 确保节奏张弛有度
- 标注建议字数
""";
    }

    public void RegisterTools(IToolCapable toolCapable)
    {
        if (_toolsInitialized) return;
        _toolsInitialized = true;

        toolCapable.RegisterTool(GetWorldSettingTool.ToolDefinition);
        toolCapable.RegisterTool(GetCharacterTool.ToolDefinition);
        toolCapable.RegisterTool(GetOutlineTool.ToolDefinition);
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
