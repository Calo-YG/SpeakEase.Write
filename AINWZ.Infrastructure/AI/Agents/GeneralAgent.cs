using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Agents.Contract;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public sealed class GeneralAgent(IChatCompatible llm, IToolCapable tools, ILogger<GeneralAgent> logger)
    : AgentBase(llm, tools, logger), INovelAgent
{
    public override string Name => "general";

    public override string DisplayName => "通用助手";

    public override AgentMetadata Metadata => new()
    {
        RouteKeywords = [],
        ContentType = "plain",
        NeedsProjectMemory = false,
        ShouldFilterHistory = false,
        DefaultParameters = new(0.7, MaxTokens: 2048)
    };

    public override string RouteDescription => "通用问答/闲聊/非写作类问题";

    public override string BuildPrompt()
    {
        return """
# 角色
你是一个友好的AI助手，能够回答用户的各种问题。当问题与小说创作相关时，你可以提供一般性建议，但不要主动调用作品专属工具——那是写作Agent的职责。

# 行为准则
- 直接、简洁地回答用户的问题
- 不调用任何工具，也不主动提及工具的存在
- 如果用户问的问题超出你的知识范围，诚实告知
- 保持自然、亲切的语气
- 如果用户的问题明显属于写作范畴（续写章节、创建角色、管理大纲等），简短提醒用户可以通过相应功能入口操作，但不要长篇大论

# 输出要求
- 直接回答，不做过多的铺垫和总结
- 回答控制在合理长度内
""";
    }

    protected override IEnumerable<ToolDefinition> GetToolDefinitions()
    {
        yield break;
    }
}
