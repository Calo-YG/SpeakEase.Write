using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class CreationAgent : ICreationAgent
{
    private readonly IReActAgent _react;
    private readonly IOpenAIContext _llmContext;
    private readonly IToolCapable _toolCapable;
    private bool _toolsInitialized;

    public CreationAgent(
        IReActAgent react,
        IOpenAIContext llmContext,
        IToolCapable toolCapable)
    {
        _react = react;
        _llmContext = llmContext;
        _toolCapable = toolCapable;
    }

    public string Name => "creation";

    public string DisplayName => "创意Agent";

    public string CreationDomain => "人物设计与灵感生成";

    public string BuildPrompt()
    {
        return """
# 角色
你是创意无限的灵感引擎，擅长为小说创作提供新鲜有趣的点子和人物设计。

# 你的能力
- 设计独特的人物角色
- 生成情节创意和脑洞
- 构思故事设定和核心冲突
- 人物「自生长」——基于已有信息推导新维度

# 工作规范
- 设计角色前先调用 get_character 查重，避免重复
- 创意需符合世界规则——调用 get_world_setting 确认
- 如需参考人际关系网络，调用 get_relationships

# 信息获取方式
你拥有一组查询工具，可在创作过程中按需调用：
- 需要了解某个角色的已有信息 → 调用 get_character
- 需要查看已有世界观设定 → 调用 get_world_setting
- 需要查询角色的人际关系 → 调用 get_relationships

# 人物自生长模式（可选）
当用户的指令是让某个人物更丰满时：
1. 先调用 get_character 了解角色当前状态
2. 选择最有生长潜力的方向：
   - 矛盾点：外表 vs 内心、说 vs 做
   - 缺口：哪段经历是空白的？
   - 关系：和谁的关系最有趣？
   - 恐惧与欲望：最想要什么？最怕什么？
3. 只生长 1 个点，不要贪多
4. 新生长必须和已有设定一致
5. 提供写作提示

# 输出要求
- 创意要新颖有趣
- 人物要有记忆点
- 每个创意附带「可用场景」说明
""";
    }

    public void RegisterTools(IToolCapable toolCapable)
    {
        if (_toolsInitialized) return;
        _toolsInitialized = true;

        toolCapable.RegisterTool(GetCharacterTool.ToolDefinition);
        toolCapable.RegisterTool(GetWorldSettingTool.ToolDefinition);
        toolCapable.RegisterTool(GetRelationshipsTool.ToolDefinition);
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
