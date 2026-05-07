using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class WorldAgent(IChatCompatible llm, IToolCapable tools) : AgentBase(llm, tools), IWorldAgent
{
    public override string Name => "world";

    public override string DisplayName => "世界观Agent";

    public string WorldDomain => "世界观构建与设定自生长";

    public override string BuildPrompt()
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
- 如有已有设定，先调用 get_world_setting 或 search_world_setting 查询，避免冲突
- 如需参考作品中的角色分布，调用 get_character、search_characters 或 get_character_list 了解角色情况
- 如需参考已有大纲和章节结构，调用 get_outline、search_outline 或 list_volumes
- 设计完成后必须调用 save_world_setting 保存设定

# 信息获取方式
你拥有一组查询和保存工具，可在构建过程中按需调用：
- 需要查看已有世界观设定 → 调用 get_world_setting
- 需要按关键词搜索世界设定 → 调用 search_world_setting
- 需要了解当前作品中的角色 → 调用 get_character、search_characters 或 get_character_list
- 需要了解已有大纲/章节分布 → 调用 get_outline、search_outline 或 list_volumes
- 设计完成后保存 → 调用 save_world_setting (传入 work_id + world_rules/geography/factions/history/summary)

# 自生长模式
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

    protected override IEnumerable<ToolDefinition> GetToolDefinitions()
    {
        yield return GetWorkInfoTool.ToolDefinition;
        yield return GetWorldSettingTool.ToolDefinition;
        yield return GetCharacterTool.ToolDefinition;
        yield return SearchCharactersTool.ToolDefinition;
        yield return GetOutlineTool.ToolDefinition;
        yield return SearchOutlineTool.ToolDefinition;
        yield return ListVolumesTool.ToolDefinition;
        yield return SaveWorldSettingTool.ToolDefinition;
        yield return SearchWorldSettingTool.ToolDefinition;
        yield return GetCharacterListTool.ToolDefinition;
    }
}
