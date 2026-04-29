using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class AuditAgent : IAuditAgent
{
    private readonly IReActAgent _react;
    private readonly IOpenAIContext _llmContext;
    private readonly IToolCapable _toolCapable;
    private bool _toolsInitialized;

    public AuditAgent(
        IReActAgent react,
        IOpenAIContext llmContext,
        IToolCapable toolCapable)
    {
        _react = react;
        _llmContext = llmContext;
        _toolCapable = toolCapable;
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
7. □ 节奏是否有问题？→ 结合大纲判断

# 信息获取方式
你拥有一组查询工具，可在审核过程中按需调用：
- 获取待审章节内容 → 调用 get_chapter
- 核实角色设定 → 调用 get_character
- 核实世界规则 → 调用 get_world_setting
- 比对大纲走向 → 调用 get_outline
- 查伏笔回收状态 → 调用 get_foreshadowing

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
        if (_toolsInitialized) return;
        _toolsInitialized = true;

        toolCapable.RegisterTool(GetChapterTool.ToolDefinition);
        toolCapable.RegisterTool(GetCharacterTool.ToolDefinition);
        toolCapable.RegisterTool(GetWorldSettingTool.ToolDefinition);
        toolCapable.RegisterTool(GetOutlineTool.ToolDefinition);
        toolCapable.RegisterTool(GetForeshadowingTool.ToolDefinition);
        toolCapable.RegisterTool(GetRecentChaptersTool.ToolDefinition);
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
