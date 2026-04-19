using SpeakEase.AI.Lib.Contract;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 内置工具的便捷注册入口。
/// 使用示例：
/// <code>
/// var agent = new ReActAgent(llmBackend);
/// BuiltinTools.RegisterAll(agent);         // 注册全部内置工具
/// BuiltinTools.RegisterCreationTools(agent); // 仅注册创作相关工具
/// </code>
/// </summary>
public static class BuiltinTools
{
    /// <summary>
    /// 注册全部内置工具到 Agent。
    /// </summary>
    public static void RegisterAll(ToolCapableBase agent)
    {
        RegisterUtilityTools(agent);
        RegisterCreationTools(agent);
    }

    /// <summary>
    /// 注册通用工具：echo、get_current_time、calculate、random_generator。
    /// </summary>
    public static void RegisterUtilityTools(ToolCapableBase agent)
    {
        EchoTool.RegisterTo(agent);
        GetCurrentTimeTool.RegisterTo(agent);
        CalculateTool.RegisterTo(agent);
        RandomGeneratorTool.RegisterTo(agent);
    }

    /// <summary>
    /// 注册创作工具：text_analyzer、generate_character_name。
    /// </summary>
    public static void RegisterCreationTools(ToolCapableBase agent)
    {
        TextAnalyzerTool.RegisterTo(agent);
        CharacterNameGeneratorTool.RegisterTo(agent);
    }
}
