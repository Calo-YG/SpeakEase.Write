using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

// 通用助手Agent：处理通用问答、闲聊和非写作类问题，具备互联网搜索能力和写作规则保存功能
// 与其他专业Agent不同，此Agent不涉及小说创作相关工具
public sealed class GeneralAgent(IChatCompatible llm, IToolCapable tools, ILogger<GeneralAgent> logger, ISkilCapable skills = null)
    : AgentBase(llm, tools, logger, skills), INovelAgent
{
    public override string Name => "general";

    public override string DisplayName => "通用助手";

    // Agent元数据：内容类型为纯文本，不需要项目记忆，不过滤历史
    public override AgentMetadata Metadata => new()
    {
        ContentType = "plain",
        NeedsProjectMemory = false,
        ShouldFilterHistory = false,
        DefaultParameters = new(0.7, MaxTokens: 4096)
    };

    public override string RouteDescription => "通用问答/闲聊/非写作类问题";

    public override PromptProfile BuildPromptProfile() => new()
    {
        Identity = "你是一个友好的通用 AI 助手，能够回答知识问题并在需要时使用外部信息。",
        Objective = "理解用户当前问题，直接给出准确、自然且可执行的回答。",
        QualityCriteria = new[] { "区分已知信息与需要查询的信息", "保持回答清晰、简洁、诚实" },
        OutputContract = "用自然语言回答，不输出内部推理过程。"
    };

    // 构建通用助手Agent的系统提示词：包含角色定义、ReAct工作模式、行为准则
    public override string BuildPrompt()
    {
        return """
# 角色
你是一个友好的AI助手，能够回答用户的各种问题。你有互联网搜索能力，可以查询最新信息和事实。

# ReAct 工作模式
你按照 推理→行动→观察 的循环模式工作：

**推理（Thought）**：每轮开始前，先在心中分析：
- 用户到底在问什么？核心需求是什么？
- 这个问题我需要查实时资料吗，还是凭已有知识就能回答？
- 如果需要搜索，用什么关键词最精准？

**行动（Action）**：根据推理结论采取行动：
- 如果问题涉及实时信息、最新新闻、事实核查或超出你知识范围的内容 → 调用 `web_search` 获取最新资料
- 如果用户提出写作规则、约束要求、写作规范（如"每章字数不超过3000""不要写血腥场面""主角不能谈恋爱"等） → 调用 `save_writing_rules` 保存规则
- 如果是闲聊或你已知的知识 → 直接回答，不调用工具
- 如果用户问题明显属于写作范畴（续写章节、创建角色、管理大纲等） → 简短提醒用户通过相应功能入口操作

**观察（Observation）**：调用工具后，仔细分析返回结果：
- 搜索结果是否充分回答了用户的问题？
- 是否需要补充更多信息或换个关键词再搜一次？
- 哪些来源最可靠、最相关？

**最终回答**：确认信息充分后给出最终回答。

# 行为准则
- 直接、简洁地回答用户的问题，不做过多的铺垫和总结
- 搜索后基于结果回答，并在回答中引用信息来源
- 回答控制在合理长度内，引用搜索来源时简洁说明即可
- 保持自然、亲切的语气
""";
    }

    // 注册通用助手Agent所需的工具：网络搜索（获取实时信息）、写作规则保存（持久化用户的写作约束）
    protected override IEnumerable<ToolDefinition> GetToolDefinitions()
    {
        yield return WebSearchTool.ToolDefinition;
        yield return SaveWritingRulesTool.ToolDefinition;
    }
}
