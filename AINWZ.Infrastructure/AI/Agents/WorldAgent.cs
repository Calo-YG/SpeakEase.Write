using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class WorldAgent : IWorldAgent
{
    private readonly IReActAgent _react;
    private readonly IOpenAIContext _llmContext;
    private readonly IToolCapable _toolCapable;
    private bool _toolsInitialized;

    public WorldAgent(
        IReActAgent react,
        IOpenAIContext llmContext,
        IToolCapable toolCapable)
    {
        _react = react;
        _llmContext = llmContext;
        _toolCapable = toolCapable;
    }

    public string Name => "world";

    public string DisplayName => "世界观Agent";

    public string WorldDomain => "世界观构建与设定自生长";

    public string BuildPrompt()
    {
        return """
# 角色
你是世界构建专家，擅长设计严谨、有深度的幻想世界设定。

# 你的能力
- 设计世界规则（魔法/科技体系）
- 构建地理与文明分布
- 设计势力与政治格局
- 创造历史与编年事件
- 基于已有设定「自生长」出合理的扩展点

# 工作规范
- 设定必须内在逻辑自洽
- 如有已有设定，先调用 get_existing_settings 查询，避免冲突
- 如需参考作品中的角色分布，调用 get_characters_in_world 了解角色情况
- 如需参考已有时间线，调用 get_timeline_events

# 信息获取方式
你拥有一组查询工具，可在构建过程中按需调用：
- 需要查看已有世界观设定 → 调用 get_existing_settings
- 需要了解当前作品中的角色 → 调用 get_characters_in_world
- 需要了解已有时间线 → 调用 get_timeline_events

# 自生长模式（可选）
当用户的指令是扩展已有设定时：
1. 先查已有设定，找到最有「生长潜力」的方向
2. 推导当前设定自然引出的新设定
3. 确保新设定与已有设定逻辑自洽
4. 提供创作提示说明新设定怎么用在故事中

# 输出要求
- 先给出骨架（规则层），再填充细节
- 设定必须内在逻辑自洽
- 每个设定点附带创作提示
""";
    }

    public void RegisterTools(IToolCapable toolCapable)
    {
        if (_toolsInitialized) return;
        _toolsInitialized = true;

        toolCapable.RegisterTool(GetWorldSettingTool.ToolDefinition);
        toolCapable.RegisterTool(GetCharacterTool.ToolDefinition);
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
