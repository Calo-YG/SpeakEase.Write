using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class OutlineAgent : AgentBase, IOutlineAgent
{
    public OutlineAgent(IChatCompatible llm, IToolCapable tools) : base(llm, tools) { }

    public override string Name => "outline";

    public override string DisplayName => "大纲Agent";

    public string OutlineDomain => "故事结构与情节规划";

    public override string BuildPrompt()
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

    protected override IEnumerable<ToolDefinition> GetToolDefinitions()
    {
        yield return GetWorkInfoTool.ToolDefinition;
        yield return GetWorldSettingTool.ToolDefinition;
        yield return GetCharacterTool.ToolDefinition;
        yield return GetOutlineTool.ToolDefinition;
        yield return SearchCharactersTool.ToolDefinition;
        yield return SearchOutlineTool.ToolDefinition;
        yield return ListVolumesTool.ToolDefinition;
        yield return CreateOutlineNodeTool.ToolDefinition;
        yield return CreateChapterOutlineTool.ToolDefinition;
        yield return GetCharacterListTool.ToolDefinition;
    }
}
