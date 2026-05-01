using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class CreationAgent : AgentBase, ICreationAgent
{
    public CreationAgent(IChatCompatible llm, IToolCapable tools) : base(llm, tools) { }

    public override string Name => "creation";

    public override string DisplayName => "创意Agent";

    public string CreationDomain => "人物设计与灵感生成";

    public override string BuildPrompt()
    {
        return """
# 角色
你是创意无限的灵感引擎，擅长为小说创作提供新鲜有趣的点子和人物设计。

# 你的能力
- 设计独特的人物角色，可调用 create_character 创建角色
- 生成情节创意和脑洞
- 构思故事设定和核心冲突
- 人物「自生长」——基于已有信息推导新维度

# 工作规范
- 设计角色前先调用 get_character 查重，避免重复
- 创意需符合世界规则——调用 get_world_setting 确认
- 如需参考人际关系网络，调用 get_relationships
- 如果角色设计完成确认可用，调用 create_character 创建到数据库

# 信息获取方式
你拥有一组查询和创建工具，可在创作过程中按需调用：
- 需要了解某个角色的已有信息 → 调用 get_character
- 需要查看已有世界观设定 → 调用 get_world_setting 或 search_world_setting
- 需要查询角色的人际关系 → 调用 get_relationships
- 需要模糊搜索/快速浏览已有角色 → 调用 search_characters 或 get_character_list
- 需要创建新角色 → 调用 create_character (传入 work_id + name + identity + personality 等)

# 人物自生长模式
当用户的指令是让某个人物更丰满时：
1. 先调用 get_character 了解角色当前状态
2. 选择最有生长潜力的方向：矛盾点/空白经历/关系/恐惧与欲望
3. 只生长 1 个点，不要贪多
4. 新生长必须和已有设定一致

# 输出要求
- 创意要新颖有趣，人物要有记忆点
- 每个创意附带「可用场景」说明
- 创建角色时严格使用 create_character 工具
""";
    }

    protected override IEnumerable<ToolDefinition> GetToolDefinitions()
    {
        yield return GetWorkInfoTool.ToolDefinition;
        yield return GetCharacterTool.ToolDefinition;
        yield return GetWorldSettingTool.ToolDefinition;
        yield return GetRelationshipsTool.ToolDefinition;
        yield return SearchCharactersTool.ToolDefinition;
        yield return CreateCharacterTool.ToolDefinition;
        yield return UpdateCharacterTool.ToolDefinition;
        yield return SearchWorldSettingTool.ToolDefinition;
        yield return GetCharacterListTool.ToolDefinition;
    }
}
